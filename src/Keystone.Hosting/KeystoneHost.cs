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
    private CapabilityDomain? _capabilityDomain;
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
    public async Task StartAsync(string configYaml)
    {
        ArgumentNullException.ThrowIfNull(configYaml);

        // 1. 配置层：条目解析（含重复 id fail-fast）
        var entries = EntryParser.Parse(configYaml);
        _tree.Clear();
        _tree.AddRange(entries);

        // 2. schema 校验（Parser 已校验条目结构/重复 id）

        // 3. manifest 校验（依赖图可达 + 无环，ADR-0007）
        var manifests = EnumerateLeaves(entries).Select(_options.ManifestProvider).ToList();
        ManifestValidator.Validate(manifests);

        // 4-5. 根 context（能力域 context 挂其下，03 §1）+ 能力域（01 §2 管理层职责，09 §2）
        _rootContext = new ContextFacade("root");
        if (_options.EnableCapabilityDomain)
        {
            _capabilityDomain = CapabilityDomain.Create(_options.CapabilityDomainName);
        }

        // 6-7. 并行加载：依赖门控（PENDING 等待）天然实现拓扑序
        await Task.WhenAll(EnumerateLeaves(entries).Select(LoadEntryAsync)).ConfigureAwait(false);

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

        _shutdown = true;
        foreach (var plugin in _plugins.ToList())
        {
            await plugin.Loader.DisposeAsync().ConfigureAwait(false);
        }

        _plugins.Clear();
        _tree.Clear();
        _failedEntries.Clear();

        if (_capabilityDomain is not null)
        {
            await _capabilityDomain.DisposeAsync().ConfigureAwait(false);
            _capabilityDomain = null;
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

    // ── Hosting API：条目 CRUD（F5）──

    /// <summary>创建条目（加载插件；返回 id；支持 parent 组）。CRUD 返回前插件已就绪（await 收敛）。</summary>
    public async Task<string> CreateEntryAsync(EntryOptions options, string? parent = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var id = options.Id ?? Guid.NewGuid().ToString("N");
        if (FindEntry(_tree, id) is not null)
        {
            throw new KeystoneException(ErrorCode.ConfigValidationFailed, $"duplicate entry id: {id}");
        }

        var entry = options with { Id = id };
        InsertEntry(_tree, entry, parent);
        if (entry.IsGroup)
        {
            EntryInit?.Invoke(this, new EntryInitEventArgs(entry)); // 组条目无加载，显式触发
        }
        else
        {
            await LoadEntryAsync(entry).ConfigureAwait(false); // 叶子：LoadEntryAsync 触发
        }

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
    }

    /// <summary>跨组移动（失败回滚：先校验目标组存在，再移动）。</summary>
    public Task MoveEntryAsync(string id, string? newParent)
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
            InsertEntry(_tree, entry, newParent);
        }
        catch (Exception)
        {
            InsertEntry(_tree, entry, null); // 回滚到根（F5 移动失败回滚）
            throw;
        }

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
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(manifest);

        var loader = await PluginLoader.CreateAsync(
            source, manifest, _registry, id => new ContextFacade(id, _rootContext)).ConfigureAwait(false);
        _plugins.Add(new HostedPlugin(manifest.Id, loader));
        EntryInit?.Invoke(this, new EntryInitEventArgs(new EntryOptions { Id = manifest.Id, Name = source.Id }));
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
    }

    // ── 内部 ──

    private async Task LoadEntryAsync(EntryOptions entry)
    {
        var manifest = _options.ManifestProvider(entry);
        var source = _options.SourceProvider(entry);

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

    private static void InsertEntry(List<EntryOptions> entries, EntryOptions entry, string? parent)
    {
        if (parent is null)
        {
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
        var updatedParent = parentEntry with { Group = [.. parentEntry.Group, entry] };
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
