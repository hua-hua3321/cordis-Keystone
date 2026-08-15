using Keystone.Config.Entries;
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
    private readonly IServiceRegistry _registry = new ServiceRegistry();
    private readonly List<HostedPlugin> _plugins = [];
    private readonly List<EntryOptions> _tree = [];
    private readonly List<Func<PatchContextEventArgs, Func<Task>, Task>> _patchContextHandlers = [];
    private readonly HashSet<string> _failedEntries = new(StringComparer.Ordinal);
    private ContextFacade? _rootContext;
    private Keystone.Config.Persistence.ConfigFileWriter? _configWriter; // DC-15：CRUD 落盘写回
    private Keystone.Runtime.Persistence.FactRetentionScheduler? _retention; // DC-18：定时 Prune
    private ConfigFileWatcher? _configWatcher; // DC-9：配置文件监听
    private volatile bool _applyingConfig; // DC-9：apply 串行化（watcher 首扫与初始化防竞态交错）
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
        _rootContext = new ContextFacade(
            "root",
            loggerFactory: _options.LoggerFactory,
            eventStore: _options.EventStore,
            logCategoryPrefix: _options.EnableCapabilityDomain ? _options.CapabilityDomainName : null);
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
        if (_options.ConfigFilePath is null)
        {
            return;
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
            EntryInit?.Invoke(this, new EntryInitEventArgs(entry)); // 组条目无加载，显式触发
        }
        else
        {
            await LoadEntryAsync(entry).ConfigureAwait(false); // 叶子：LoadEntryAsync 触发
        }

        ScheduleWriteBack();
        return id;
    }

    /// <summary>删除条目（卸载插件 + 从树移除）。</summary>
    public async Task RemoveEntryAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var hosted = _plugins.FirstOrDefault(p => string.Equals(p.EntryId, id, StringComparison.Ordinal));
        if (hosted is not null)
        {
            EntryDisposing?.Invoke(this, new EntryDisposingEventArgs(id, active: true));
            await hosted.Loader.DisposeAsync().ConfigureAwait(false);
            _plugins.Remove(hosted);
        }

        RemoveFromTree(_tree, id);
        NotifyConfigUpdate();
        ScheduleWriteBack();
    }

    /// <summary>跨组移动/排序（失败回滚：先校验目标组存在，再移动；position 指定插入位置）。</summary>
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

    // ── H2 编程式挂载 ──

    /// <summary>编程式挂载（H2 端到端）：挂载 → 门控 → 运行；经 RemoveEntryAsync 卸载。</summary>
    public async Task MountAsync(PluginSource source, PluginManifest manifest)
    {
        ThrowIfShuttingDown();
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(manifest);

        var loader = await PluginLoader.CreateAsync(
            source, manifest, _registry, id => new ContextFacade(id, _rootContext)).ConfigureAwait(false);
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
        var source = _options.SourceProvider(entry);
        var config = await ResolvePluginConfigAsync(entry).ConfigureAwait(false);

        // DC-6：先卸载旧实例（Unregister 释放 provides 注册）再启动新——避免同名注册冲突
        // （原顺序先启动新会因 Register rebind 报错或误删新注册）
        var hosted = _plugins.FirstOrDefault(p => string.Equals(p.EntryId, id, StringComparison.Ordinal));
        if (hosted is not null)
        {
            await hosted.Loader.DisposeAsync().ConfigureAwait(false);
            _plugins.Remove(hosted);
        }

        var loader = await PluginLoader.CreateAsync(
            source, manifest, _registry, ctxId => new ContextFacade(ctxId, _rootContext), config).ConfigureAwait(false);

        _plugins.Add(new HostedPlugin(id, loader));
        EntryInit?.Invoke(this, new EntryInitEventArgs(entry));
        NotifyConfigUpdate();
    }

    /// <summary>
    /// 插件配置热更新（G-C8）：更新条目 config → PatchContext 瀑布（可否决）→ 重载。
    /// 对齐 08 §6.1 "仅 config 变 → 热更新"分级 + ADR-0005 决策 3。
    /// </summary>
    public async Task UpdatePluginAsync(string id, object? newConfig)
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
            ScheduleWriteBack(); // 应用成功才落盘（否决不写）
        }).ConfigureAwait(false);
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
    public async Task ApplyConfigAsync(IReadOnlyList<EntryOptions> newTree)
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

            // 移除 → 卸载
            foreach (var id in diff.Removed)
            {
                await RemoveEntryAsync(id).ConfigureAwait(false);
            }

            // 新增 → 加载
            foreach (var entry in diff.Added)
            {
                await CreateEntryAsync(entry).ConfigureAwait(false);
            }

            // disabled 翻转 → 挂起/恢复
            foreach (var entry in diff.DisabledFlips)
            {
                await SetEntryDisabledAsync(entry.Id!, entry.Disabled == true).ConfigureAwait(false);
            }

            // 结构变 → 冷重启
            foreach (var entry in diff.StructurallyChanged)
            {
                PluginReloading?.Invoke(this, new PluginReloadingEventArgs(entry.Id!));
                ReplaceEntry(_tree, entry);
                await ReloadPluginAsync(entry.Id!).ConfigureAwait(false);
            }

            // 仅 config 变 → 热更新（瀑布可否决；内部含写回）
            foreach (var entry in diff.ConfigChanged)
            {
                PluginUpdating?.Invoke(this, new PluginUpdatingEventArgs(entry.Id!, entry.Config));
                await UpdatePluginAsync(entry.Id!, entry.Config).ConfigureAwait(false);
            }

            ConfigReloaded?.Invoke(this, new ConfigReloadedEventArgs(diff.ChangedIds));
        }
        finally
        {
            _applyingConfig = false;
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
            await ApplyConfigAsync(tree).ConfigureAwait(false);
        });
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

        var loader = await PluginLoader.CreateAsync(
            source, manifest, _registry, id => new ContextFacade(id, _rootContext), config).ConfigureAwait(false);
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
