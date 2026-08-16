using Keystone.Core;
using Keystone.Core.Errors;
using Keystone.Runtime.Context;
using Keystone.Runtime.Plugins.Manifest;
using Keystone.Runtime.Plugins.Services;

namespace Keystone.Runtime.Plugins.Lifecycle;

/// <summary>
/// 插件运行时：生命周期状态机（ADR-0005）+ 依赖门控（ADR-0007）+ quiesce 卸载闸门。
///
/// 依赖语义：inject 服务未全就绪 → PENDING（事件驱动等待，非轮询）；服务出现 → 自动 LOADING/ACTIVE；
/// 依赖消失 → 依赖方走完整卸载闸门。启动失败 → FAILED（持有错误，可 restart）。
/// </summary>
public sealed class PluginRuntime : IAsyncDisposable
{
    private readonly PluginManifest _manifest;
    private readonly Func<IPluginContext, IPlugin> _pluginFactory;
    private readonly IServiceDiscovery _discovery;
    private readonly IReadOnlyDictionary<string, string>? _isolateMap; // 名 → realm（门控域视图；与 context 工厂同源）
    private readonly Func<string, IPluginContext> _contextFactory;
    private readonly IReadOnlyDictionary<string, object?> _config;
    private readonly TimeSpan _quiesceTimeout;
    private readonly TimeSpan _dependencyTimeout;
    private readonly Keystone.Runtime.Events.IEventBus? _externalBus;
    private readonly Lock _lock = new();

    private PluginLifecycleState _state = PluginLifecycleState.Pending;
    private Exception? _error;
    private IPlugin? _plugin;
    private IPluginContext? _context;
    private IDisposable? _dependencySubscription;
    private TaskCompletionSource? _settled;

    public PluginRuntime(
        PluginManifest manifest,
        Func<IPluginContext, IPlugin> pluginFactory,
        IServiceDiscovery discovery,
        Func<string, IPluginContext> contextFactory,
        IReadOnlyDictionary<string, string>? isolateMap = null,
        IReadOnlyDictionary<string, object?>? config = null,
        TimeSpan? quiesceTimeout = null,
        TimeSpan? dependencyTimeout = null,
        Keystone.Runtime.Events.IEventBus? eventBus = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(pluginFactory);
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(contextFactory);

        _manifest = manifest;
        _pluginFactory = pluginFactory;
        _discovery = discovery;
        _contextFactory = contextFactory;
        _isolateMap = isolateMap;
        _config = config ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        _quiesceTimeout = quiesceTimeout ?? new KeystoneSettings().QuiesceTimeout;
        _dependencyTimeout = dependencyTimeout ?? new KeystoneSettings().DependencyWaitTimeout;
        _externalBus = eventBus; // DC-11：无 context 阶段（依赖等待/超时）的生命周期事实出口

        // 依赖门控（ADR-0007 决策 3）：依赖消失 → 卸载；依赖重现 → 自动重启（G-C2 re-arm）。
        // 订阅保持整个 runtime 生命周期（StopCoreAsync 不销毁，DisposeAsync 才清理）。
        // 批量变更键（可用 = 值存在的投影）：命中 inject 名 → 重评（不信任投递时刻的可用性快照，消除竞态）
        _dependencySubscription = discovery.Subscribe(keys =>
        {
            var relevant = false;
            foreach (var key in keys)
            {
                if (manifest.Inject.Contains(key.Name, StringComparer.Ordinal))
                {
                    relevant = true;
                    break;
                }
            }

            if (!relevant)
            {
                return;
            }

            if (DependenciesSatisfied() && State is PluginLifecycleState.Disposed or PluginLifecycleState.Unloading)
            {
                // G-C2：依赖重现（值回到 store）→ 自动重启（对齐 Cordis epoch 驱动）
                _ = StartAsync();
            }
            else if (!DependenciesSatisfied() && State == PluginLifecycleState.Active)
            {
                // 依赖消失（值删）→ 依赖方走完整卸载闸门
                _ = StopCoreAsync();
            }
        });
    }

    /// <summary>当前状态。</summary>
    public PluginLifecycleState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    /// <summary>插件上下文（ACTIVE 后可用）。</summary>
    public IPluginContext? Context
    {
        get
        {
            lock (_lock)
            {
                return _context;
            }
        }
    }

    /// <summary>状态迁移事件（internal/status 对应物）。</summary>
    public event EventHandler<LifecycleStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// 启动：依赖未就绪 → PENDING 等待（事件驱动）；就绪 → LOADING → ACTIVE；失败 → FAILED。
    /// 返回的任务在到达 ACTIVE/FAILED 后完成。
    /// </summary>
    public Task StartAsync() => StartCoreAsync();

    /// <summary>稳定等待（对齐 Cordis fiber.await）：ACTIVE 完成；FAILED 重抛启动错误。</summary>
    public async Task AwaitAsync()
    {
        PluginLifecycleState state;
        Exception? error;
        Task settled;
        lock (_lock)
        {
            state = _state;
            error = _error;
            settled = _settled?.Task ?? Task.CompletedTask;
        }

        if (state is PluginLifecycleState.Active or PluginLifecycleState.Failed or PluginLifecycleState.Disposed)
        {
            if (error is not null)
            {
                throw error;
            }

            return;
        }

        await settled.ConfigureAwait(false);
        lock (_lock)
        {
            error = _error;
        }

        if (error is not null)
        {
            throw error;
        }
    }

    /// <summary>完整卸载闸门（quiesce，ADR-0005 决策 2）→ DISPOSED。显式调用 → 销毁依赖订阅（终态）。</summary>
    public async Task StopAsync()
    {
        lock (_lock)
        {
            if (_state is PluginLifecycleState.Disposed or PluginLifecycleState.Unloading)
            {
                return;
            }
        }

        await StopCoreAsync().ConfigureAwait(false);
        _dependencySubscription?.Dispose();
        _dependencySubscription = null; // 显式停止 = 终态，不再 re-arm（G-C2 仅依赖消失路径保留）
    }

    /// <summary>restart：走完整卸载闸门 + 重新启动（FAILED 恢复/显式运维，ADR-0005 决策 3）。</summary>
    public async Task RestartAsync()
    {
        lock (_lock)
        {
            if (_state is not (PluginLifecycleState.Active or PluginLifecycleState.Failed))
            {
                throw new KeystoneException(
                    ErrorCode.LifecycleInvalidState,
                    $"cannot restart plugin '{_manifest.Id}' from state '{_state}'");
            }
        }

        if (State == PluginLifecycleState.Active)
        {
            await StopCoreAsync().ConfigureAwait(false);
        }

        lock (_lock)
        {
            _error = null;
            _state = PluginLifecycleState.Pending;
            _settled = null;
        }

        await StartCoreAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _dependencySubscription?.Dispose();
        _dependencySubscription = null;
    }

    private async Task StartCoreAsync()
    {
        lock (_lock)
        {
            // G-C2：Disposed 也允许启动（依赖重现自动重启的恢复路径）
            if (_state is not (PluginLifecycleState.Pending or PluginLifecycleState.Failed or PluginLifecycleState.Disposed))
            {
                throw new KeystoneException(
                    ErrorCode.LifecycleInvalidState,
                    $"cannot start plugin '{_manifest.Id}' from state '{_state}'");
            }

            _error = null;
            _settled = null;
            _state = PluginLifecycleState.Pending;
        }

        SetState(PluginLifecycleState.Pending);
        if (!await AwaitDependenciesOrFailAsync().ConfigureAwait(false))
        {
            return; // 依赖超时 → 已置 FAILED
        }

        SetState(PluginLifecycleState.Loading);
        try
        {
            await InitializePluginAsync().ConfigureAwait(false);
            SetState(PluginLifecycleState.Active);
            await EmitLifecycleFactAsync(new Keystone.Runtime.Events.PluginStartedFact(_manifest.Id)).ConfigureAwait(false);
            CompleteSettled();
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                // CA1031：任何初始化失败（插件代码可能抛任意异常）都进入 FAILED 态，属设计语义
                _error = new KeystoneException(
                    ErrorCode.LifecycleLoadFailed,
                    $"plugin '{_manifest.Id}' failed to initialize",
                    ex);
            }

            SetState(PluginLifecycleState.Failed);
            await EmitLifecycleFactAsync(
                new Keystone.Runtime.Events.PluginFailedFact(_manifest.Id, ex.Message)).ConfigureAwait(false);
            CompleteSettled();
        }
    }

    /// <summary>LOADING 阶段：创建 context/插件实例 → 初始化 → 注册 provides。</summary>
    private async Task InitializePluginAsync()
    {
        IPluginContext context;
        IPlugin plugin;
        lock (_lock)
        {
            _context = _contextFactory(_manifest.Id);
            context = _context;
            _plugin = _pluginFactory(context);
            plugin = _plugin;
        }

        await plugin.InitializeAsync(context, _config).ConfigureAwait(false);

        // 18 §2 CA-1：值即注册——manifest.provides 必须 init 期兑现（可用 = 值存在）。
        // 声明未 Provide → 明确 FAILED（点名服务），消灭"门控放行但 Get 落空"静默晚失败。
        var missing = _manifest.Provides
            .Where(service => !_discovery.IsAvailable(service, Realm(service)))
            .ToList();
        if (missing.Count > 0)
        {
            throw new KeystoneException(
                ErrorCode.LifecycleLoadFailed,
                $"plugin '{_manifest.Id}' declared provides [{string.Join(", ", missing)}] but did not Provide them during initialization");
        }
    }

    private bool DependenciesSatisfied()
        => _manifest.Inject.All(service => _discovery.IsAvailable(service, Realm(service)));

    /// <summary>门控域视图（18 §2 CA-1）：isolateMap 命中 → 该域；否则 ""（默认共享）。与 context 工厂注入的 map 同源等价。</summary>
    private string Realm(string serviceName)
        => _isolateMap is not null && _isolateMap.TryGetValue(serviceName, out var realm) ? realm : string.Empty;

    /// <summary>DC-5：等待依赖；超时 → FAILED（ADR-0007：不无限 PENDING）。返回 false = 已置 FAILED。</summary>
    private async Task<bool> AwaitDependenciesOrFailAsync()
    {
        try
        {
            await WaitForDependenciesAsync().ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                _error = ex is KeystoneException ke
                    ? ke
                    : new KeystoneException(ErrorCode.GatingDependencyTimeout, $"plugin '{_manifest.Id}' dependency wait failed", ex);
            }

            SetState(PluginLifecycleState.Failed);
            await EmitLifecycleFactAsync(
                new Keystone.Runtime.Events.PluginFailedFact(_manifest.Id, _error.Message)).ConfigureAwait(false);
            CompleteSettled();
            return false;
        }
    }

    /// <summary>
    /// DC-11（ADR-0009/03 §4）：插件生命周期事实——经插件 context 总线（无 context 阶段用外部总线）；
    /// emit 携带存储时持久化（尽力写）。总线缺失（未配置）= 无事实发布。
    /// </summary>
    private Task EmitLifecycleFactAsync<TFact>(TFact fact) where TFact : Keystone.Runtime.Events.IFactEvent
    {
        var bus = Context is { } pluginContext ? pluginContext.Context.Events : _externalBus;
        return bus?.EmitAsync(fact, Context?.Context) ?? Task.CompletedTask;
    }

    /// <summary>事件驱动等待依赖（ADR-0007 决策 3：服务可用性事件 → 重新检查，非轮询）。</summary>
    private async Task WaitForDependenciesAsync()
    {
        if (DependenciesSatisfied())
        {
            return;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IDisposable? subscription = null;
        subscription = _discovery.Subscribe(_ =>
        {
            if (DependenciesSatisfied())
            {
                subscription?.Dispose();
                tcs.TrySetResult();
            }
        });

        // 订阅后复查，防注册发生在订阅前检查与订阅之间的竞态
        if (DependenciesSatisfied())
        {
            subscription.Dispose();
            tcs.TrySetResult();
        }

        // DC-5（ADR-0007 风险表）：依赖永不就绪 → 启动超时 → FAILED（不无限 PENDING 挂起）
        using var timeoutCts = new CancellationTokenSource(_dependencyTimeout);
        try
        {
            await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            subscription?.Dispose();
            throw new KeystoneException(
                ErrorCode.GatingDependencyTimeout,
                $"plugin '{_manifest.Id}' dependency wait timed out after {_dependencyTimeout.TotalSeconds}s: "
                + string.Join(", ", _manifest.Inject.Where(s => !_discovery.IsAvailable(s, Realm(s)))));
        }
    }

    private async Task StopCoreAsync()
    {
        SetState(PluginLifecycleState.Unloading);
        // ① 收敛 effect disposer（逆序，ADR-0005 决策 2 步骤 3），超时强制（风险缓解：不无限等待）
        if (_context is not null)
        {
            await WithTimeoutAsync(_context.Context.DisposeEffectsAsync(), "effect quiesce").ConfigureAwait(false);
        }

        // ② 插件 disposer
        if (_plugin is not null)
        {
            await WithTimeoutAsync(_plugin.DisposeAsync(), "plugin dispose").ConfigureAwait(false);
        }

        // G-C3 值卸载 = 可用性摘除（值即注册，18 §2 CA-1）：dispose 删键 + Removed 通知 → 依赖方重评
        if (_context is not null && _context is ContextFacade facade)
        {
            facade.RemoveOwnedServices();
        }

        // G-C2：依赖订阅保留（依赖重现 → 自动重启）；DisposeAsync（真正销毁）才清理
        lock (_lock)
        {
            _plugin = null;
        }

        SetState(PluginLifecycleState.Disposed);
        CompleteSettled();
    }

    private async Task WithTimeoutAsync(Task task, string phase)
    {
        var timeout = Task.Delay(_quiesceTimeout);
        if (await Task.WhenAny(task, timeout).ConfigureAwait(false) == timeout)
        {
            return; // 超时强制进入下一步（ADR-0005 风险缓解）
        }

        await task.ConfigureAwait(false);
    }

    private void SetState(PluginLifecycleState state)
    {
        lock (_lock)
        {
            _state = state;
        }

        StateChanged?.Invoke(this, new LifecycleStateChangedEventArgs(state));
    }

    private void CompleteSettled()
    {
        lock (_lock)
        {
            _settled?.TrySetResult();
        }
    }
}
