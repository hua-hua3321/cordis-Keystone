using Keystone.Config.Entries;
using Microsoft.Extensions.Logging;
using Keystone.Config.Validation;
using Keystone.Core.Errors;
using Keystone.Runtime.Actors;
using Keystone.Runtime.Context;
using Keystone.Runtime.Plugins.Lifecycle;
using Keystone.Runtime.Plugins.Loading;
using Keystone.Runtime.Plugins.Manifest;
using Keystone.Runtime.Plugins.Services;

namespace Keystone.Hosting;

/// <summary>
/// Keystone 宿主（09 管理层）：8 步启动、全局 quiesce、hosting API（CRUD/挂载/状态/配置）、
/// 管理面事件（F9）。插件加载序 = 依赖门控自动拓扑（ADR-0007：PENDING 等待天然排序）。
/// </summary>
public sealed class KeystoneHost : IAsyncDisposable
{
    private readonly KeystoneHostOptions _options;
    private IServiceDiscovery _discovery = null!; // InitializeAsync 内投影 root store（值层唯一事实源）
    private readonly List<HostedPlugin> _plugins = [];
    private readonly List<EntryOptions> _tree = [];
    private readonly List<Func<PatchContextEventArgs, Func<Task>, Task>> _patchContextHandlers = [];
    private readonly HashSet<string> _failedEntries = new(StringComparer.Ordinal);
    private ContextFacade? _rootContext;
    private Keystone.Config.Persistence.ConfigFileWriter? _configWriter; // DC-15：CRUD 落盘写回
    private Keystone.Runtime.Persistence.FactRetentionScheduler? _retention; // DC-18：定时 Prune
    private ConfigFileWatcher? _configWatcher; // DC-9：配置文件监听
    private PluginFileWatcher? _pluginWatcher; // CA-2：插件源文件监听
    private volatile bool _applyingConfig; // DC-9：apply 串行化（watcher 首扫与初始化防竞态交错）
    private bool _suppressWriteBack; // CA-15：事务期写回抑制（save=false 路径）
    private Keystone.Runtime.Logging.RingBufferLoggerProvider? _ringBufferLogs; // CA-12：ServiceOptions logger 接线产物（诊断面）
    private Microsoft.Extensions.Logging.ILoggerFactory? _ownedLoggerFactory; // CA-12：自建 factory（Create 静态类型即接口；Shutdown 经此字段 Dispose）
    private CapabilityDomain? _capabilityDomain;
    private IReadOnlyList<string> _uncollectedPlugins = [];
    private bool _shutdown;

    public KeystoneHost(KeystoneHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>条目创建（loader/entry-init，F9）。</summary>
    public event EventHandler<EntryInitEventArgs>? EntryInit;

    /// <summary>条目卸载（loader/partial-dispose，F9）。</summary>
    public event EventHandler<EntryDisposingEventArgs>? EntryDisposing;

    /// <summary>配置写回前（loader/config-update，F9）。</summary>
    public event EventHandler<ConfigUpdateEventArgs>? ConfigUpdate;

    /// <summary>进程重启请求钩子（对齐 exit 信号，F9）。</summary>
    public Func<Task>? ExitRequested { get; set; }

    // ── 启动 / 关闭 ──

    /// <summary>8 步启动（09 §2）：解析条目 → schema 校验 → manifest 校验 → 根 context → 并行加载（门控拓扑）→ 就绪。</summary>
    /// <summary>运行期 patch 应用（CA-5，P61）：解析后、manifest 校验前（对齐 Cordis patch 在 schema 前生效）。</summary>
    private IReadOnlyList<EntryOptions> ApplyConfigPatches(IReadOnlyList<EntryOptions> entries)
        => _options.ConfigPatches is { Count: > 0 } patches
            ? Keystone.Config.Entries.EntryPatcher.Apply(entries, patches)
            : entries;

    /// <summary>文件入口启动（CA-6，P60，对齐 Cordis include Service.init ENOENT+initial 先写再读）：
    /// 要求 ConfigFilePath 已配置——文件不存在且 <see cref="KeystoneHostOptions.InitialEntries"/> 非空 →
    /// 写入 initial 再读；文件已存在 → initial 忽略（现网配置优先）；两者皆无 → 明确报错。</summary>
    public async Task StartFromFileAsync()
    {
        ThrowIfShuttingDown();
        var path = _options.ConfigFilePath
            ?? throw new KeystoneException(ErrorCode.ConfigValidationFailed,
                "ConfigFilePath is not configured — no file to start from");

        if (!File.Exists(path))
        {
            if (_options.InitialEntries is not { Count: > 0 } initial)
            {
                throw new KeystoneException(ErrorCode.ConfigValidationFailed,
                    $"config file not found and no InitialEntries configured: {path}");
            }

            _configWriter ??= new Keystone.Config.Persistence.ConfigFileWriter(path);
            await _configWriter.EnsureInitialAsync(initial).ConfigureAwait(false); // 存在则跳过（幂等）
        }

        var yaml = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        await StartAsync(yaml).ConfigureAwait(false);
    }

    /// <summary>ServiceOptions["logger"] → RingBufferLoggerProvider 工厂（CA-12，P60）：
    /// { defaultLevel, capacity, levels: {category→level} }；显式 LoggerFactory 优先时不会被调用。</summary>
    private Microsoft.Extensions.Logging.ILoggerFactory? BuildServiceLoggerFactory()
    {
        if (_options.ServiceOptions is null
            || !_options.ServiceOptions.TryGetValue("logger", out var loggerOptions))
        {
            return null; // 无服务选项 → 保持 NullLoggerFactory 兜底（现状）
        }

        var capacity = loggerOptions.TryGetValue("capacity", out var capacityValue)
            && int.TryParse(capacityValue?.ToString(), System.Globalization.CultureInfo.InvariantCulture, out var parsedCapacity) ? parsedCapacity : 1000;
        var defaultLevel = loggerOptions.TryGetValue("defaultLevel", out var defaultLevelValue)
            && Enum.TryParse<Microsoft.Extensions.Logging.LogLevel>(defaultLevelValue?.ToString(), out var parsedLevel)
            ? parsedLevel
            : (Microsoft.Extensions.Logging.LogLevel?)null;
        var overrides = new Dictionary<string, Microsoft.Extensions.Logging.LogLevel>(StringComparer.Ordinal);
        if (loggerOptions.TryGetValue("levels", out var levelsValue)
            && levelsValue is IReadOnlyDictionary<string, object?> levels)
        {
            foreach (var (category, levelValue) in levels)
            {
                if (Enum.TryParse<Microsoft.Extensions.Logging.LogLevel>(levelValue?.ToString(), out var level))
                {
                    overrides[category] = level;
                }
            }
        }

        _ringBufferLogs = new Keystone.Runtime.Logging.RingBufferLoggerProvider(
            capacity, overrides, sinks: null, defaultLevel);
        _ownedLoggerFactory = LoggerFactory.Create(builder => builder.AddProvider(_ringBufferLogs));
        return _ownedLoggerFactory;
    }

    public Task StartAsync(string configYaml)
    {
        ArgumentNullException.ThrowIfNull(configYaml);
        return StartAsync([configYaml]);
    }

    /// <summary>
    /// 分层启动（DC-7，08 §4）：多 YAML 层按序叠加（base → profile → 用户 patch → 运行期 overlay）——
    /// patch 按 id 合并（提供的字段覆盖、未提供保留）、显式 insert 插入、层内重复 id fail-fast；
    /// 每层独立解析（含 DC-8 静态插值），叠加以条目 id 为主键（EntryTree.ApplyLayers）。
    /// </summary>
    public async Task StartAsync(IReadOnlyList<string> layerYamls)
    {
        ArgumentNullException.ThrowIfNull(layerYamls);
        if (layerYamls.Count == 0)
        {
            throw new KeystoneException(ErrorCode.ConfigValidationFailed, "at least one config layer is required");
        }

        // 1. 配置层：逐层解析（DC-8 插值按层展开）→ 分层叠加（08 §4）
        var interpolator = BuildInterpolator();
        var layers = layerYamls
            .Select(layer => EntryParser.Parse(layer, interpolator))
            .ToList();
        var entries = EntryTree.ApplyLayers(layers);

        entries = ApplyConfigPatches(entries); // CA-5：读后 patch（校验前）

        _tree.Clear();
        _tree.AddRange(entries);

        // 2. schema 校验（Parser 已校验条目结构/重复 id）

        // 3. manifest 校验（依赖图可达 + 无环，ADR-0007）——active 叶子
        //    （DC-16：挂起条目不参与运行，其 inject 引用放宽——恢复加载时再校验）
        var manifests = EnumerateActiveLeaves(entries).Select(_options.ManifestProvider).ToList();
        ManifestValidator.Validate(manifests);

        // 4-5. 根 context（能力域 context 挂其下，03 §1）+ 能力域（01 §2 管理层职责，09 §2）
        // DC-11：根总线携带事实存储——插件 context（子链共享总线）的生命周期事实自动持久化
        // DC-20：根 context 接 LoggerFactory + 域前缀（category = {域}/{插件 ID}，05 §5）
        // CA-12（P60）：未注入 LoggerFactory 且 ServiceOptions["logger"] 存在 → RingBuffer 接线
        // （修复：provider 构造一直支持 overrides/defaultLevel/capacity 但无人接线——NullLogger 兜底绕过）
        var loggerFactory = _options.LoggerFactory ?? BuildServiceLoggerFactory();
        _rootContext = new ContextFacade(
            "root",
            loggerFactory: loggerFactory,
            eventStore: _options.EventStore,
            logCategoryPrefix: _options.EnableCapabilityDomain ? _options.CapabilityDomainName : null);
        _discovery = new InMemoryServiceDiscovery(_rootContext.Services); // 发现层投影（P57-T4）

        if (_options.EnableCapabilityDomain)
        {
            _capabilityDomain = CapabilityDomain.Create(_options.CapabilityDomainName);
        }

        // 6-7. 并行加载：依赖门控（PENDING 等待）天然实现拓扑序
        // DC-16：disabled 挂起条目（含父组继承）不参与加载拓扑
        await Task.WhenAll(EnumerateActiveLeaves(entries).Select(LoadEntryAsync)).ConfigureAwait(false);

        // DC-18：事实保留策略 → 定时 Prune（随宿主启停；失败降级续跑）
        if (_options.RetentionPolicy is { } retention && _options.EventStore is { } eventStore)
        {
            _retention = new Keystone.Runtime.Persistence.FactRetentionScheduler(
                eventStore, retention, _options.PruneInterval);
            _retention.Start();
        }

        // 8. 就绪
        _shutdown = false;
    }

    /// <summary>全局 quiesce（09 §4）：逐插件 quiesce（含 ALC 卸载）；幂等。</summary>
    public async Task ShutdownAsync()
    {
        if (_shutdown)
        {
            return;
        }

        // DC-3（09 §4）：① 入口拒绝（后续 CreateEntry/Mount/Reload 直接拒绝）
        _shutdown = true;

        // DC-15：排空写回队列（挂起的 CRUD 变更落盘后再 quiesce）
        try
        {
            await FlushConfigAsync().ConfigureAwait(false);
        }
        catch (KeystoneException)
        {
            // 写回失败（占用/只读）：不阻断关闭（08 §6.3 readonly——报错不崩溃）
        }

        // ② 逐插件 quiesce（ADR-0005 五步闸门），带总关闭超时 + 未收敛审计（09 §4 第 6 步）
        var uncollected = new List<string>();
        using var cts = new CancellationTokenSource(_options.ShutdownTimeout);
        var tasks = _plugins.Select(async plugin =>
        {
            try
            {
                await plugin.Loader.DisposeAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                uncollected.Add(plugin.EntryId); // 超时未收敛 → 记录（可观测性审计）
            }
        }).ToList();
        try
        {
            await Task.WhenAll(tasks).WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 总超时强制退出：记录全部未收敛插件
            uncollected.AddRange(_plugins.Select(p => p.EntryId));
        }

        if (uncollected.Count > 0)
        {
            _uncollectedPlugins = [.. uncollected];
        }

        _plugins.Clear();
        _tree.Clear();
        _failedEntries.Clear();

        // ③ 停能力域监督树（不再重启，防关闭期"复活"，09 §4 第 4 步）
        if (_capabilityDomain is not null)
        {
            await _capabilityDomain.DisposeAsync().ConfigureAwait(false);
            _capabilityDomain = null;
        }

        DisposeOwnedLoggerFactory(); // CA-12：自建 factory（ServiceOptions 产物）
    }

    /// <summary>释放自建 logger factory（CA-12：ServiceOptions 接线产物；嵌入方注入的不归本宿主管）。</summary>
    private void DisposeOwnedLoggerFactory()
    {
        _ownedLoggerFactory?.Dispose();
        _ownedLoggerFactory = null;
    }

    /// <summary>关闭超时未收敛的插件（诊断审计，09 §4 第 6 步）。</summary>
    public IReadOnlyList<string> UncollectedPlugins => _uncollectedPlugins;

    private void ThrowIfShuttingDown()
    {
        if (_shutdown)
        {
            throw new KeystoneException(ErrorCode.LifecycleInvalidState, "host is shutting down");
        }
    }

    /// <summary>能力域（01 §2 管理层职责）：跨域请求入口。StartAsync 后可用；未启用时为 null。</summary>
    public CapabilityDomain? GetCapabilityDomain() => _capabilityDomain;

    /// <summary>
    /// 宿主事件总线（P22，B4 公开事件面）：StartAsync 后可用（root context 共享总线，ID-08）。
    /// 插件订阅经各自 context 注册到同一总线；宿主可经此发布全局事件（观察者插件收到）。
    /// </summary>
    public Keystone.Runtime.Events.IEventBus? Events => _rootContext?.Events;

    private void NotifyConfigUpdate()
        => ConfigUpdate?.Invoke(this, new ConfigUpdateEventArgs(DumpConfig()));

    // ── DC-15：CRUD 落盘写回管线（09 §5/08 §6.3）──

    /// <summary>CRUD 变更防抖写回（ConfigFilePath 未配置 = 纯内存，无操作）。</summary>
    private void ScheduleWriteBack()
    {
        if (_options.ConfigFilePath is null || _suppressWriteBack)
        {
            return; // CA-15：save=false 的事务内子操作不写回（防回环）
        }

        NotifyConfigUpdate(); // 配置写回前通知（F9 loader/config-update）
        _configWriter ??= new Keystone.Config.Persistence.ConfigFileWriter(_options.ConfigFilePath);
        _configWriter.ScheduleWrite(DumpConfig());
    }

    /// <summary>冲刷写回队列（测试/嵌入方在关键点确保落盘）。</summary>
    public Task FlushConfigAsync()
        => _configWriter?.FlushAsync() ?? Task.CompletedTask;

    // ── Hosting API：条目 CRUD（F5）──

    /// <summary>创建条目（加载插件；返回 id；支持 parent 组 + position 插入位置）。CRUD 返回前插件已就绪（await 收敛）。</summary>
    public async Task<string> CreateEntryAsync(EntryOptions options, string? parent = null, int? position = null)
    {
        ThrowIfShuttingDown();
        ArgumentNullException.ThrowIfNull(options);

        var id = options.Id ?? Guid.NewGuid().ToString("N");
        if (FindEntry(_tree, id) is not null)
        {
            throw new KeystoneException(ErrorCode.ConfigValidationFailed, $"duplicate entry id: {id}");
        }

        var entry = options with { Id = id };
        InsertEntry(_tree, entry, parent, position);
        if (entry.IsGroup)
        {
            // CA-10（P58）：运行期建组 → 逐叶加载（不再是空壳组）；挂起继承 DC-16——
            // EnumerateActiveLeaves([entry]) 含组自身 disabled 检查（disabled 组整树不加载）
            foreach (var child in EnumerateActiveLeaves([entry]))
            {
                await LoadEntryAsync(child).ConfigureAwait(false); // 失败隔离语义沿用（叶 FAILED 不阻断兄弟）
            }

            EntryInit?.Invoke(this, new EntryInitEventArgs(entry)); // 组条目无加载，显式触发
        }
        else
        {
            await LoadEntryAsync(entry).ConfigureAwait(false); // 叶子：LoadEntryAsync 触发
        }

        ScheduleWriteBack();
        return id;
    }

    /// <summary>删除条目（卸载插件 + 从树移除）。组条目 = 逆序逐叶级联卸载（CA-10，P58）。</summary>
    public async Task RemoveEntryAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        // 树内条目：组 → 级联卸载；树外托管插件（H2 MountAsync 不进树）→ 直接卸载（宽容语义保持）
        if (FindEntry(_tree, id) is { IsGroup: true } entry)
        {
            // CA-10（P58）：删组 = 整子树卸载——逆序（后声明先卸，对齐 Cordis group remove 逐子卸载序）。
            // 修复前只删树不卸插件 → 整组孤儿续跑（仅 ApplyConfigAsync 路径被 diff 扁平化间接弥补）
            foreach (var leaf in EnumerateLeaves(entry.Group!).Reverse())
            {
                await DisposeHostedAsync(leaf.Id!).ConfigureAwait(false);
            }
        }
        else
        {
            await DisposeHostedAsync(id).ConfigureAwait(false);
        }

        RemoveFromTree(_tree, id);
        NotifyConfigUpdate();
        ScheduleWriteBack();
    }

    /// <summary>卸载已托管插件（EntryDisposing → loader.DisposeAsync → 移除托管记录）；未托管 = 无操作。</summary>
    private async Task DisposeHostedAsync(string id)
    {
        var hosted = _plugins.FirstOrDefault(p => string.Equals(p.EntryId, id, StringComparison.Ordinal));
        if (hosted is null)
        {
            return;
        }

        EntryDisposing?.Invoke(this, new EntryDisposingEventArgs(id, active: true));
        await hosted.Loader.DisposeAsync().ConfigureAwait(false);
        _plugins.Remove(hosted);
    }

    /// <summary>跨组移动/排序（失败回滚：先校验目标组存在，再移动；position 指定插入位置）。
    /// 纯树操作：插件不重载不迁移——成员 context 链与 realm 谱系不变（CA-10 注明与 Cordis 差异：
    /// Cordis 组移动重挂 fiber；Keystone 组只承载声明谱系，运行时拓扑不受移动影响）。</summary>
    public Task MoveEntryAsync(string id, string? newParent, int? position = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var entry = FindEntry(_tree, id)
            ?? throw new KeystoneException(ErrorCode.ConfigValidationFailed, $"entry not found: {id}");

        if (newParent is not null && FindEntry(_tree, newParent) is not { } parent)
        {
            throw new KeystoneException(ErrorCode.ConfigValidationFailed, $"parent group not found: {newParent}");
        }

        RemoveFromTree(_tree, id);
        try
        {
            InsertEntry(_tree, entry, newParent, position);
        }
        catch (Exception)
        {
            InsertEntry(_tree, entry, null, null); // 回滚到根（F5 移动失败回滚）
            throw;
        }

        ScheduleWriteBack();
        return Task.CompletedTask;
    }

    /// <summary>嵌套 id 解析（`:` 分隔跨子树，对齐 EntryTree.resolve）。</summary>
    public EntryOptions ResolveEntry(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var parts = id.Split(':', 2);
        var root = FindEntry(_tree, parts[0])
            ?? throw new KeystoneException(ErrorCode.ConfigValidationFailed, $"entry not found: {id}");
        if (parts.Length == 1)
        {
            return root;
        }

        var child = FindEntry(root.Group ?? [], parts[1])
            ?? throw new KeystoneException(ErrorCode.ConfigValidationFailed, $"entry not found: {id}");
        return child;
    }

    /// <summary>当前生效配置树（对齐 harness --dump-config）。</summary>
    public IReadOnlyList<EntryOptions> DumpConfig() => [.. _tree];

    /// <summary>插件状态查询（PENDING/ACTIVE/FAILED...）。</summary>
    public PluginLifecycleState GetPluginState(string entryId)
    {
        if (_failedEntries.Contains(entryId))
        {
            return PluginLifecycleState.Failed; // G-C1：配置校验失败 → FAILED（隔离语义）
        }

        var hosted = _plugins.FirstOrDefault(p => string.Equals(p.EntryId, entryId, StringComparison.Ordinal))
            ?? throw new KeystoneException(ErrorCode.GatingServiceNotFound, $"plugin not loaded: {entryId}");
        return hosted.Loader.Runtime.State;
    }

    /// <summary>环形缓冲日志 provider（CA-12：ServiceOptions["logger"] 接线产物；未接线 = null）。
    /// 诊断入口：<see cref="Keystone.Runtime.Logging.RingBufferLoggerProvider.GetSnapshot"/>。</summary>
    public Keystone.Runtime.Logging.RingBufferLoggerProvider? RingBufferLogs => _ringBufferLogs;

    // ── H2 编程式挂载 ──

    /// <summary>编程式挂载（H2 端到端）：挂载 → 门控 → 运行；经 RemoveEntryAsync 卸载。</summary>
    public async Task MountAsync(PluginSource source, PluginManifest manifest)
    {
        ThrowIfShuttingDown();
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(manifest);

        var loader = await PluginLoader.CreateAsync(
            source, manifest, _discovery, id => new ContextFacade(id, _rootContext)).ConfigureAwait(false);
        _plugins.Add(new HostedPlugin(manifest.Id, loader));
        EntryInit?.Invoke(this, new EntryInitEventArgs(new EntryOptions { Id = manifest.Id, Name = source.Id }));
    }

    // ── 热更新（G-C8，09 §5 ReloadPlugin/UpdatePlugin 承诺）──

    /// <summary>
    /// 插件冷重启（G-C8）：重新编译源码 + 换新 loader（新 ALC + 新 runtime）→ 旧 quiesce + ALC.Unload。
    /// 对齐 08 §6.1 "name/inject/group 变 → 冷重启"分级。
    /// </summary>
    public async Task ReloadPluginAsync(string id)
    {
        ThrowIfShuttingDown();
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var entry = FindEntry(_tree, id)
            ?? throw new KeystoneException(ErrorCode.ConfigValidationFailed, $"entry not found: {id}");
        var manifest = _options.ManifestProvider(entry);
        // CA-2（P62）：冷重启重取源（获取端抽象优先——文件形态读到最新代码；静态 SourceProvider 兜底）
        var source = _options.PluginSource is { } pluginSource
            ? await pluginSource.FetchAsync(manifest).ConfigureAwait(false)
            : _options.SourceProvider(entry);
        var config = await ResolvePluginConfigAsync(entry).ConfigureAwait(false);

        // DC-6：先卸载旧实例（Unregister 释放 provides 注册）再启动新——避免同名注册冲突
        // （原顺序先启动新会因 Register rebind 报错或误删新注册）
        var hosted = _plugins.FirstOrDefault(p => string.Equals(p.EntryId, id, StringComparison.Ordinal));
        if (hosted is not null)
        {
            await hosted.Loader.DisposeAsync().ConfigureAwait(false);
            _plugins.Remove(hosted);
        }

        var isolateMap = BuildIsolateMap(id);
        var loader = await PluginLoader.CreateAsync(
            source, manifest, _discovery, ctxId => new ContextFacade(ctxId, _rootContext, isolateMap: isolateMap), config, isolateMap).ConfigureAwait(false);

        _plugins.Add(new HostedPlugin(id, loader));
        EntryInit?.Invoke(this, new EntryInitEventArgs(entry));
        NotifyConfigUpdate();
    }

    /// <summary>
    /// 插件配置热更新（G-C8）：更新条目 config → PatchContext 瀑布（可否决）→ 重载。
    /// 对齐 08 §6.1 "仅 config 变 → 热更新"分级 + ADR-0005 决策 3。
    /// </summary>
    public async Task UpdatePluginAsync(string id, object? newConfig, bool save = true)
    {
        ThrowIfShuttingDown();
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var entry = FindEntry(_tree, id)
            ?? throw new KeystoneException(ErrorCode.ConfigValidationFailed, $"entry not found: {id}");
        var updated = entry with { Config = newConfig };

        // 瀑布可否决（F9 PatchContext：不调 apply 即否决）
        await PatchContextAsync(updated, async () =>
        {
            ReplaceEntry(_tree, updated);
            await ReloadPluginAsync(id).ConfigureAwait(false);
            if (save)
            {
                ScheduleWriteBack(); // 应用成功才落盘（否决不写；CA-15 save=false 内存态不落盘）
            }
        }).ConfigureAwait(false);
    }

    /// <summary>组合更新（CA-4，P59，对齐 Cordis tree.update）：一次调用改选项 + 跨组移动 + position。
    /// 判定：结构键（name/inject/isolate）与 parent 均不变 → 热路径（PatchContext 瀑布 + 热更新）；
    /// 结构变或跨组 → 冷路径（冷重启）。移动记账 (源组, 原下标)——任一步失败回插原位置
    /// （修复 MoveEntryAsync 回滚只回根的偏差）。</summary>
    public async Task UpdateEntryAsync(string id, Keystone.Config.Entries.EntryOptions options, string? parent = null, int? position = null)
    {
        ThrowIfShuttingDown();
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(options);

        var current = FindEntry(_tree, id)
            ?? throw new KeystoneException(ErrorCode.ConfigValidationFailed, $"entry not found: {id}");
        if (parent is not null && FindEntry(_tree, parent) is not { } parentEntry)
        {
            throw new KeystoneException(ErrorCode.ConfigValidationFailed, $"parent group not found: {parent}");
        }

        var updated = options with { Id = id };
        var structuralChanged = !string.Equals(StructuralKeyOf(current), StructuralKeyOf(updated), StringComparison.Ordinal);
        var (sourceParent, sourceIndex) = LocateEntry(_tree, id);
        var parentChanged = !string.Equals(sourceParent, parent, StringComparison.Ordinal);

        // 移动记账：(源父 id, 原下标)——失败回插精确原位
        var moved = false;
        if (parentChanged)
        {
            RemoveFromTree(_tree, id);
            try
            {
                InsertEntry(_tree, updated, parent, position);
                moved = true;
            }
            catch (Exception)
            {
                InsertEntry(_tree, current, sourceParent, sourceIndex); // 回插原位置（非根）
                throw;
            }
        }

        var runtimeTorn = false; // D-4：reload 是否已"先卸旧"（DC-6）——失败时运行体已失需复原
        try
        {
            await ApplyEntryUpdateAsync(id, updated, structuralChanged || parentChanged, torn => runtimeTorn = torn)
                .ConfigureAwait(false);
            ScheduleWriteBack();
        }
        catch (Exception)
        {
            RestoreEntry(id, current, moved, sourceParent, sourceIndex);
            if (runtimeTorn)
            {
                await RestoreRuntimeAsync(id).ConfigureAwait(false);
            }

            throw;
        }
    }

    /// <summary>D-4（19 号审计 LD-9，对齐 entry.ts:232-243）：失败用旧条目重启插件
    /// （修复前只复原树不复原运行时 → 失败后插件处于已卸载态）。树已复原，尽力而为——
    /// 复原失败不上抛（原异常为准，树态是唯一承诺）。</summary>
    private async Task RestoreRuntimeAsync(string id)
    {
        // CA1031：兜底复原尽力而为——插件可抛任意异常，吞掉以保原异常上抛
#pragma warning disable CA1031
        try
        {
            await ReloadPluginAsync(id).ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
#pragma warning restore CA1031
    }

    /// <summary>热/冷路径执行（CA-4）：结构变或跨组 → 冷重启；否则 PatchContext 瀑布热更新。
    /// torn 回调：即将"先卸旧"（DC-6）时置真——调用方失败路径据此复原运行时（D-4）。</summary>
    private async Task ApplyEntryUpdateAsync(
        string id, Keystone.Config.Entries.EntryOptions updated, bool coldPath, Action<bool> torn)
    {
        if (coldPath)
        {
            ReplaceEntry(_tree, updated);
            PluginReloading?.Invoke(this, new PluginReloadingEventArgs(id));
            torn(true);
            await ReloadPluginAsync(id).ConfigureAwait(false);
            return;
        }

        await PatchContextAsync(updated, async () =>
        {
            ReplaceEntry(_tree, updated);
            torn(true);
            await ReloadPluginAsync(id).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <summary>失败复原（CA-4）：已移动 → 移回原位再回旧值；未移动 → 原地回旧值。</summary>
    private void RestoreEntry(string id, Keystone.Config.Entries.EntryOptions current, bool moved, string? sourceParent, int sourceIndex)
    {
        if (moved)
        {
            RemoveFromTree(_tree, id);
            InsertEntry(_tree, current, sourceParent, sourceIndex);
        }
        else
        {
            ReplaceEntry(_tree, current);
        }
    }

    /// <summary>条目结构键（与 ConfigDiffer 同语义：name/inject/isolate 生效域；CA-4 判定用）。</summary>
    private static string StructuralKeyOf(Keystone.Config.Entries.EntryOptions e)
        => $"{e.Name}|{string.Join(",", e.Inject)}";

    /// <summary>定位条目 (父组 id, 组内下标)；根级 = (null, 根下标)；未找到 = (null, -1)。</summary>
    private static (string? Parent, int Index) LocateEntry(IReadOnlyList<Keystone.Config.Entries.EntryOptions> entries, string id, string? parent = null)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i].Id, id, StringComparison.Ordinal))
            {
                return (parent, i);
            }

            if (entries[i].Group is { } children)
            {
                var nested = LocateEntry(children, id, entries[i].Id);
                if (nested.Index != -1)
                {
                    return nested;
                }
            }
        }

        return (null, -1);
    }

    // ── DC-9：文件变更 → 重载 → diff → 逐条目更新（08 §6 触发管线）──

    /// <summary>配置重载完成（diff 后；负载 = 变更条目 id 集）。</summary>
    public event EventHandler<ConfigReloadedEventArgs>? ConfigReloaded;

    /// <summary>条目热更新前（仅 config 变路径，08 §6.1）。</summary>
    public event EventHandler<PluginUpdatingEventArgs>? PluginUpdating;

    /// <summary>条目冷重启前（name/inject/isolate 变路径，08 §6.1）。</summary>
    public event EventHandler<PluginReloadingEventArgs>? PluginReloading;

    /// <summary>
    /// 应用新配置树（DC-9，08 §6 管线）：diff（按条目 id）→ 按 §6.1 分级逐条目执行——
    /// 新增 → 加载；移除 → 卸载；仅 config 变 → 热更新（UpdatePluginAsync，瀑布可否决）；
    /// name/inject/isolate 变 → 冷重启（ReloadPluginAsync）；disabled 翻转 → 挂起/恢复
    /// （SetEntryDisabledAsync）。应用后写回落盘（事务性刷新语义：新树 = 真源）。
    /// </summary>
    public async Task ApplyConfigAsync(IReadOnlyList<EntryOptions> newTree, bool save = true)
    {
        ArgumentNullException.ThrowIfNull(newTree);

        while (_applyingConfig)
        {
            await Task.Delay(10).ConfigureAwait(false); // apply 串行化（08 §6.3）
        }

        _applyingConfig = true;
        try
        {
            var diff = ConfigDiffer.Diff([.. _tree], newTree);
            if (diff.IsEmpty)
            {
                return; // deepEqual 相等即跳过（08 §6.1）
            }

            await ApplyDiffTransactionallyAsync(diff, save).ConfigureAwait(false);
            ConfigReloaded?.Invoke(this, new ConfigReloadedEventArgs(diff.ChangedIds));
        }
        finally
        {
            _applyingConfig = false;
        }
    }

    /// <summary>diff 事务应用（CA-3，P59）：各阶段收集失败——单错抛原因、多错 AggregateException；
    /// 任一失败 → 逆序撤销本次已成功的变更；回滚失败聚合进同一异常上抛
    /// （对齐 Cordis group 事务；diff 增量模式下回滚面更小——旧条目未动无需重建）。
    /// 每步前 ThrowIfShuttingDown（Disposal owns termination：卸载中的组更新不回滚）。</summary>
    private async Task ApplyDiffTransactionallyAsync(ConfigDiff diff, bool save)
    {
        var applied = new List<Func<Task>>(); // 逆序回滚动作栈（成功一步记一步）
        var failures = new List<Exception>();
        _suppressWriteBack = !save; // CA-15：save=false（watcher 路径）→ 子操作写回抑制（防回环）
        try
        {
            var oldEntries = diff.ConfigChanged.Concat(diff.StructurallyChanged)
                .Select(e => FindEntry(_tree, e.Id!))
                .Where(e => e is not null)
                .ToDictionary(e => e!.Id!, e => e!, StringComparer.Ordinal); // 变更前旧条目（回滚素材）

            await ApplyRemovedStageAsync(diff.Removed, failures, applied).ConfigureAwait(false);

            await CollectPerItemAsync(diff.Added, failures, entry => ApplyAddedEntryAsync(entry, applied)).ConfigureAwait(false);

            await CollectPerItemAsync(diff.DisabledFlips, failures, entry => ApplyDisabledFlipAsync(entry, applied)).ConfigureAwait(false);

            // 结构变（两阶段，P57-T5）：阶段级收集（组替换与叶子重载共享树状态，逐条中断会留半替换树）
            await CollectStepAsync(failures, async () =>
            {
                ThrowIfShuttingDown();
                // P0-3（19 号审计 LD-3）：逐条目登记 undo（树替换即记）——中途失败也复原已替换树，
                // 不留半应用态（对齐 Cordis 失败全量重建）
                await ApplyStructuralChangesAsync(diff.StructurallyChanged, oldEntries, applied).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await CollectPerItemAsync(diff.ConfigChanged, failures,
                entry => ApplyConfigEntryAsync(entry, oldEntries, applied, save)).ConfigureAwait(false);
        }
        finally
        {
            _suppressWriteBack = false; // 异常/回滚路径同样解除抑制
        }

        if (failures.Count > 0)
        {
            _suppressWriteBack = true; // 回滚动作同样不写回（树最终回到调用方期望态）
            try
            {
                await RollbackAsync(applied, failures).ConfigureAwait(false);
            }
            finally
            {
                _suppressWriteBack = false;
            }
        }
    }

    /// <summary>Removed 阶段（D-5，对齐 Cordis group.ts:95-101 回滚面=全量重建含 Removed）：
    /// 删除前捕获原条目+归属；删除后登记复合 undo（组先于子按声明序整体重建并重载）。</summary>
    private async Task ApplyRemovedStageAsync(
        IReadOnlyList<string> removed, List<Exception> failures, List<Func<Task>> applied)
    {
        var removedUndo = removed
            .Select(id => (Id: id, Entry: FindEntry(_tree, id), Loc: LocateEntry(_tree, id)))
            .Where(u => u.Entry is not null)
            .ToList();

        await CollectPerItemAsync(removed, failures, async id =>
        {
            ThrowIfShuttingDown();
            await RemoveEntryAsync(id).ConfigureAwait(false);
        }).ConfigureAwait(false);

        if (removedUndo.Count == 0)
        {
            return;
        }

        var undo = removedUndo; // 闭包捕获
        applied.Add(async () =>
        {
            foreach (var (rid, rentry, (rparent, rindex)) in undo)
            {
                if (FindEntry(_tree, rid) is not null)
                {
                    continue; // 已随组重建连带恢复
                }

                InsertEntry(_tree, rentry!, rparent, rindex);
                foreach (var leaf in EnumerateActiveLeaves([rentry!]))
                {
                    await LoadEntryAsync(leaf).ConfigureAwait(false); // 失活态不重载（原状态保持）
                }
            }
        });
    }

    /// <summary>新增条目应用（CA-3 + P0-1/P0-2）：
    /// P0-1——按 diff 携带的谱系进父组（修复前：扁平集无 parent → 子叶被插到根）；
    /// P0-2——跳过已随父组 Create 连带加载的子条目（修复前：再 Create 撞 duplicate id → 新组+子必失败）；
    /// 失败清树——CreateEntryAsync 先插树后加载，编译失败须撤树（不留半应用条目）。</summary>
    private async Task ApplyAddedEntryAsync(AddedEntry added, List<Func<Task>> applied)
    {
        ThrowIfShuttingDown();
        if (FindEntry(_tree, added.Entry.Id!) is not null)
        {
            return; // P0-2：Added 扁平集含组+全部子——组已连带加载，去重
        }

        var addedId = added.Entry.Id!;
        try
        {
            await CreateEntryAsync(added.Entry, added.ParentId, added.Position).ConfigureAwait(false);
        }
        catch (Exception)
        {
            await RemoveEntryAsync(addedId).ConfigureAwait(false); // 撤树（未托管插件 = 纯树移除）
            throw;
        }

        applied.Add(() => RemoveEntryAsync(addedId));
    }

    /// <summary>disabled 翻转应用（CA-3）：成功记翻回原值。</summary>
    private async Task ApplyDisabledFlipAsync(EntryOptions entry, List<Func<Task>> applied)
    {
        ThrowIfShuttingDown();
        await SetEntryDisabledAsync(entry.Id!, entry.Disabled == true).ConfigureAwait(false);
        var flipId = entry.Id!;
        var flipBack = entry.Disabled != true;
        applied.Add(() => SetEntryDisabledAsync(flipId, flipBack));
    }

    /// <summary>热更新应用（CA-3，瀑布可否决）：成功记回滚（旧 config）。</summary>
    private async Task ApplyConfigEntryAsync(
        EntryOptions entry,
        IReadOnlyDictionary<string, EntryOptions> oldEntries,
        List<Func<Task>> applied,
        bool save = true)
    {
        ThrowIfShuttingDown();
        PluginUpdating?.Invoke(this, new PluginUpdatingEventArgs(entry.Id!, entry.Config));
        await UpdatePluginAsync(entry.Id!, entry.Config, save).ConfigureAwait(false);
        var oldConfig = oldEntries.GetValueOrDefault(entry.Id!)?.Config;
        var changedId = entry.Id!;
        applied.Add(() => UpdatePluginAsync(changedId, oldConfig));
    }

    /// <summary>逐条目执行 + 失败收集（CA-3：一条失败不阻断同批其余条目——对齐 Cordis allSettled 并行语义）。</summary>
    private static async Task CollectPerItemAsync<T>(
        IReadOnlyList<T> items, List<Exception> failures, Func<T, Task> action)
    {
        foreach (var item in items)
        {
            try
            {
                await action(item).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add(ex);
            }
        }
    }

    /// <summary>单阶段执行 + 失败收集（CA-3：异常经列表上抛聚合，不吞——与 ConfigFileWatcher 同款豁免）。</summary>
    private static async Task CollectStepAsync(
        List<Exception> failures, Func<Task> step)
    {
#pragma warning disable CA1031 // 事务应用链可抛任意异常（编译/校验/加载），收集聚合是本方法职责
        try
        {
            await step().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            failures.Add(ex);
        }
#pragma warning restore CA1031
    }






    /// <summary>逆序回滚：撤销本次已成功变更；回滚失败聚合进原失败列表一起上抛（CA-3，P59）。</summary>
    private static async Task RollbackAsync(List<Func<Task>> applied, List<Exception> failures)
    {
        for (var i = applied.Count - 1; i >= 0; i--)
        {
#pragma warning disable CA1031 // 回滚链可抛任意异常，聚合上抛是本方法职责（不吞）
            try
            {
                await applied[i]().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                failures.Add(ex); // 回滚失败聚合（不吞）
            }
#pragma warning restore CA1031
        }

        throw failures.Count == 1
            ? failures[0]
            : new AggregateException(failures);
    }

    /// <summary>结构变应用（两阶段，P57-T5）：先整体替换树——组级 isolate 声明先落位，
    /// 叶子重载时 BuildIsolateMap 读到的已是新谱系；再逐叶子冷重启（组条目本身无运行时）。</summary>
    /// <summary>结构变更应用（CA-3 + P0-3）：逐条目"替换树 → 即刻登记 undo → 重载"——
    /// 重载失败时树也已登记复原（修复前：undo 整步后统一登记 → 中途失败留半应用态）。</summary>
    private async Task ApplyStructuralChangesAsync(
        IReadOnlyList<Keystone.Config.Entries.EntryOptions> changed,
        IReadOnlyDictionary<string, Keystone.Config.Entries.EntryOptions> oldEntries,
        List<Func<Task>> applied)
    {
        foreach (var entry in changed)
        {
            ThrowIfShuttingDown();
            var oldEntry = oldEntries.GetValueOrDefault(entry.Id!);
            ReplaceEntry(_tree, entry);
            if (oldEntry is { } prev)
            {
                var undoId = entry.Id!;
                applied.Add(async () =>
                {
                    ReplaceEntry(_tree, prev);
                    if (!prev.IsGroup)
                    {
                        await ReloadPluginAsync(undoId).ConfigureAwait(false);
                    }
                });
            }

            if (!entry.IsGroup)
            {
                PluginReloading?.Invoke(this, new PluginReloadingEventArgs(entry.Id!));
                await ReloadPluginAsync(entry.Id!).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 启用配置文件监听（DC-9，08 §6 触发源）：文件变更（防抖合并）→ 重读文件 →
    /// <see cref="ApplyConfigAsync"/>。失败保留旧树（08 §6"最后好树保持运行"）。
    /// </summary>
    public void EnableConfigWatch()
    {
        ThrowIfShuttingDown();
        var path = _options.ConfigFilePath
            ?? throw new KeystoneException(ErrorCode.ConfigValidationFailed,
                "ConfigFilePath is not configured — nothing to watch");
        if (_configWatcher is not null)
        {
            return; // 幂等
        }

        _configWatcher = new ConfigFileWatcher(path, async () =>
        {
            var yaml = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            var tree = Keystone.Config.Entries.EntryParser.Parse(yaml);
            // CA-15：文件已是新值——不写回（防回环写）
            await ApplyConfigAsync(tree, save: false).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// 启用插件源文件监听（CA-2，P62，08 §6 第一触发源；与 EnableConfigWatch 对称，opt-in）：
    /// 源文件变更（防抖合并）→ 按 manifest.Main 文件名匹配 active 条目 → 冷重启
    /// <see cref="ReloadPluginAsync"/>（重编译 + 换 ALC + quiesce 旧实例）。
    /// 编译失败 → 插件 FAILED（隔离语义，不崩宿主）。仅 LocalPluginSource roots 形态可监听。
    /// </summary>
    public void EnablePluginWatch()
    {
        ThrowIfShuttingDown();
        if (_pluginWatcher is not null)
        {
            return; // 幂等
        }

        var roots = (_options.PluginSource as Keystone.Runtime.Plugins.Loading.LocalPluginSource)?.Roots
            ?? throw new KeystoneException(ErrorCode.ConfigValidationFailed,
                "PluginSource is not a LocalPluginSource — no plugin roots to watch");
        if (roots.Length == 0)
        {
            throw new KeystoneException(ErrorCode.ConfigValidationFailed, "no plugin roots configured to watch");
        }

        _pluginWatcher = new PluginFileWatcher(roots[0], file => OnPluginSourceChangedAsync(file, roots));
    }

    private async Task OnPluginSourceChangedAsync(string file, string[] roots)
    {
        var fileName = Path.GetFileName(file);
        // 按 manifest.Main 文件名匹配 active 条目（roots 下约定布局）
        var matches = EnumerateActiveLeaves([.. _tree])
            .Where(entry => string.Equals(
                Path.GetFileName(_options.ManifestProvider(entry).Main),
                fileName,
                StringComparison.Ordinal))
            .Select(entry => entry.Id!)
            .ToList();
        foreach (var id in matches)
        {
            ThrowIfShuttingDown();
            PluginReloading?.Invoke(this, new PluginReloadingEventArgs(id)); // 双轨事件（F9）
            await ReloadPluginAsync(id).ConfigureAwait(false); // 冷重启管线（编译失败 → FAILED 隔离）
        }
    }

    // ── 管理面事件：PatchContext（waterfall 可否决，F9）──

    /// <summary>订阅上下文补丁瀑布（不调 next 即否决）。</summary>
    public IDisposable SubscribePatchContext(Func<PatchContextEventArgs, Func<Task>, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _patchContextHandlers.Add(handler);
        return new Subscription(() => _patchContextHandlers.Remove(handler));
    }

    /// <summary>执行上下文补丁（瀑布链，可否决）。</summary>
    public async Task PatchContextAsync(EntryOptions entry, Func<Task> apply)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(apply);

        Func<Task> next = apply;
        for (var i = _patchContextHandlers.Count - 1; i >= 0; i--)
        {
            var handler = _patchContextHandlers[i];
            var inner = next;
            next = () => handler(new PatchContextEventArgs(entry), inner);
        }

        await next().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await ShutdownAsync().ConfigureAwait(false);
        _configWriter?.Dispose(); // DC-15：写回器随宿主释放
        _retention?.Dispose(); // DC-18：定时 Prune 随宿主停止
        _configWatcher?.Dispose(); // DC-9：配置监听随宿主停止
    }

    // ── 内部 ──

    /// <summary>条目生效 isolate map（P57-T5）：沿谱系外→内累积（组声明 #groupId / 叶自声明 #leafId / @label）。
    /// 同一 map 同时注入 context 工厂与 PluginRuntime（门控域 == 解析域，18 §2 第 3 步）。</summary>
    private IReadOnlyDictionary<string, string>? BuildIsolateMap(string entryId)
        => IsolateMapResolver.Resolve(FindEntryPath(_tree, entryId) ?? []);

    /// <summary>根→目标的谱系链（含目标自身；用于 isolate 生效域解析）。</summary>
    private static List<EntryOptions>? FindEntryPath(IReadOnlyList<EntryOptions> entries, string id, List<EntryOptions>? path = null)
    {
        path ??= [];
        foreach (var entry in entries)
        {
            path.Add(entry);
            if (string.Equals(entry.Id, id, StringComparison.Ordinal))
            {
                return path;
            }

            if (entry.Group is { } children && FindEntryPath(children, id, path) is { } found)
            {
                return found;
            }

            path.RemoveAt(path.Count - 1);
        }

        return null;
    }

    private async Task LoadEntryAsync(EntryOptions entry)
    {
        var manifest = _options.ManifestProvider(entry);
        var source = _options.PluginSource is { } pluginSource
            ? await pluginSource.FetchAsync(manifest).ConfigureAwait(false) // DC-19：获取端抽象优先
            : _options.SourceProvider(entry);

        // G-C1 配置注入（16-cordis-gap-review）：schema 校验 + 默认值补齐后传入插件。
        // 校验失败 → 该插件 FAILED（09 §2 隔离语义：插件失败不整域回滚），不阻断其他插件。
        IReadOnlyDictionary<string, object?>? config;
        try
        {
            config = await ResolvePluginConfigAsync(entry).ConfigureAwait(false);
        }
        catch (Keystone.Core.Errors.KeystoneException ex) when (string.Equals(ex.Code, Keystone.Core.Errors.ErrorCode.ConfigValidationFailed, StringComparison.Ordinal))
        {
            _failedEntries.Add(entry.Id!);
            EntryInit?.Invoke(this, new EntryInitEventArgs(entry));
            return;
        }

        var isolateMap = BuildIsolateMap(entry.Id!);
        var loader = await PluginLoader.CreateAsync(
            source, manifest, _discovery, id => new ContextFacade(id, _rootContext, isolateMap: isolateMap), config, isolateMap).ConfigureAwait(false);
        _plugins.Add(new HostedPlugin(entry.Id!, loader));
        EntryInit?.Invoke(this, new EntryInitEventArgs(entry)); // 统一加载入口（F9）
    }

    /// <summary>G-C1：条目 config → 插件 config（无 schema 声明则原始直传；有则校验+默认值）。</summary>
    private async Task<IReadOnlyDictionary<string, object?>?> ResolvePluginConfigAsync(EntryOptions entry)
    {
        if (entry.Config is null)
        {
            return null;
        }

        var schema = _options.ConfigSchemaProvider(entry);
        if (schema is null)
        {
            return entry.Config as IReadOnlyDictionary<string, object?>
                ?? new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = entry.Config };
        }

        var resolver = new ConfigResolver();
        var resolved = await resolver
            .ResolveAsync(entry.Config, schema, _options.ConfigFilters, CancellationToken.None)
            .ConfigureAwait(false);
        return resolved as IReadOnlyDictionary<string, object?>
            ?? new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = resolved };
    }

    /// <summary>DC-8：按选项构建静态插值器（任一提供者配置才启用；否则 null = 不插值）。</summary>
    private Keystone.Config.Interpolation.StaticInterpolator? BuildInterpolator()
        => _options.EnvProvider is not null || _options.FileProvider is not null
            ? new Keystone.Config.Interpolation.StaticInterpolator(
                _options.EnvProvider ?? (_ => null),
                _options.FileProvider ?? (_ => null))
            : null;

    /// <summary>挂起/恢复条目（DC-16，08 §3）：disabled=true 卸载但树保留（挂起不删）；false 加载恢复。</summary>
    public async Task SetEntryDisabledAsync(string id, bool disabled)
    {
        ThrowIfShuttingDown();
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var entry = FindEntry(_tree, id)
            ?? throw new KeystoneException(ErrorCode.ConfigValidationFailed, $"entry not found: {id}");
        var updated = entry with { Disabled = disabled ? true : null };

        var hosted = _plugins.FirstOrDefault(p => string.Equals(p.EntryId, id, StringComparison.Ordinal));
        if (disabled)
        {
            if (hosted is not null)
            {
                EntryDisposing?.Invoke(this, new EntryDisposingEventArgs(id, active: true));
                await hosted.Loader.DisposeAsync().ConfigureAwait(false); // 卸载（条目保留）
                _plugins.Remove(hosted);
            }
        }
        else if (hosted is null && !updated.IsGroup)
        {
            await LoadEntryAsync(updated).ConfigureAwait(false); // 改回即恢复（08 §3）
        }

        ReplaceEntry(_tree, updated);
        ScheduleWriteBack();
    }

    private static IEnumerable<EntryOptions> EnumerateLeaves(IEnumerable<EntryOptions> entries)
    {
        foreach (var entry in entries)
        {
            if (entry.Group is not null)
            {
                foreach (var child in EnumerateLeaves(entry.Group))
                {
                    yield return child;
                }
            }
            else
            {
                yield return entry;
            }
        }
    }

    /// <summary>
    /// DC-16（08 §3）：挂起条目枚举——disabled=true（自身或祖先）的叶子排除；
    /// 组条目自身永不被挂起（其子树跟随组 disabled 继承）。
    /// </summary>
    private static IEnumerable<EntryOptions> EnumerateActiveLeaves(IEnumerable<EntryOptions> entries)
    {
        foreach (var entry in entries)
        {
            if (entry.Disabled == true)
            {
                continue; // 父组挂起 → 整个子树不参与加载
            }

            if (entry.Group is not null)
            {
                foreach (var child in EnumerateActiveLeaves(entry.Group))
                {
                    yield return child;
                }
            }
            else
            {
                yield return entry;
            }
        }
    }

    private static EntryOptions? FindEntry(IEnumerable<EntryOptions> entries, string id)
    {
        foreach (var entry in entries)
        {
            if (string.Equals(entry.Id, id, StringComparison.Ordinal))
            {
                return entry;
            }

            if (entry.Group is not null && FindEntry(entry.Group, id) is { } child)
            {
                return child;
            }
        }

        return null;
    }

    private static void RemoveFromTree(List<EntryOptions> entries, string id)
    {
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (string.Equals(entry.Id, id, StringComparison.Ordinal))
            {
                entries.RemoveAt(i);
                return;
            }

            if (entry.Group is not null)
            {
                var group = entry.Group.ToList();
                var before = group.Count;
                RemoveFromTree(group, id);
                if (group.Count != before)
                {
                    entries[i] = entry with { Group = group }; // 组条目不可变列表 → 重建
                    return;
                }
            }
        }
    }

    private static void InsertEntry(List<EntryOptions> entries, EntryOptions entry, string? parent, int? position)
    {
        if (parent is null)
        {
            if (position is { } index && index >= 0 && index < entries.Count)
            {
                entries.Insert(index, entry); // 指定插入位置（09 §5 position 参数）
                return;
            }

            entries.Add(entry);
            return;
        }

        var parentEntry = FindEntry(entries, parent)
            ?? throw new KeystoneException(ErrorCode.ConfigValidationFailed, $"parent group not found: {parent}");
        if (parentEntry.Group is null)
        {
            throw new KeystoneException(ErrorCode.ConfigValidationFailed, $"entry '{parent}' is not a group");
        }

        // EntryOptions.Group 是不可变列表——重建组条目
        List<EntryOptions> children;
        if (position is { } childIndex && childIndex >= 0 && childIndex < parentEntry.Group.Count)
        {
            children = [.. parentEntry.Group];
            children.Insert(childIndex, entry);
        }
        else
        {
            children = [.. parentEntry.Group, entry];
        }

        var updatedParent = parentEntry with { Group = children };
        ReplaceEntry(entries, updatedParent);
    }

    private static void ReplaceEntry(List<EntryOptions> entries, EntryOptions updated)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i].Id, updated.Id, StringComparison.Ordinal))
            {
                entries[i] = updated;
                return;
            }
        }
    }

    private sealed class HostedPlugin
    {
        public HostedPlugin(string entryId, PluginLoader loader)
        {
            EntryId = entryId;
            Loader = loader;
        }

        public string EntryId { get; }

        public PluginLoader Loader { get; }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;
        private bool _disposed;

        public Subscription(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _unsubscribe();
            GC.SuppressFinalize(this);
        }
    }
}
