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
/// 依赖消失 → 完整卸载闸门后落 <see cref="PluginLifecycleState.Pending"/>（P2-13，19 号审计 CF-9：
/// 可 re-arm 存活态，对齐 fiber.ts:611-623——显式 StopAsync 才是终态 Disposed）；
/// FAILED 随依赖变化重评（依赖到位 → 自动重启）。启动失败 → FAILED（持有错误，可 restart）。
///
/// P66（19 号审计）：P1-1 AwaitAsync 真等待；P1-2 停止取消在途依赖等待（无延迟 FAILED 翻转）；
/// P1-3 StopCoreAsync 互斥门（并发停恰一次 quiesce/dispose）；P1-4 rearm 全路径无未观察异常；
/// P1-5 Loading 期依赖消失 → 加载收敛后卸载（对齐 fiber.ts:665-672 epoch 对比）；
/// D-7 提供者 ACTIVE 才放行依赖方（init 期 provide 暂存，ACTIVE 提交补发）；
/// P2-16 provides 兑现 = 属主本人提供。
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
    private readonly SemaphoreSlim _stopGate = new(1, 1); // P1-3：并发停止互斥（第二停并入/直返）
    private int _startBusy; // 启动在途标志（rearm/显式并发启动幂等收口）
    private int _rearmedPending; // P2-13：依赖消失卸载后落 PENDING 的标记（区别于初始等待 PENDING）

    private PluginLifecycleState _state = PluginLifecycleState.Pending;
    private Exception? _error;
    private IPlugin? _plugin;
    private IPluginContext? _context;
    private IDisposable? _dependencySubscription;
    private TaskCompletionSource? _settled;
    private CancellationTokenSource _lifecycleCts = new(); // P1-2：停止取消在途依赖等待（每次启动重建）

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

        _dependencySubscription = WireDependencyRearm(discovery);
    }

    /// <summary>依赖门控（ADR-0007 决策 3）+ P2-13 rearm 订阅接线（构造器拆分，MA0051）。</summary>
    private IDisposable WireDependencyRearm(IServiceDiscovery discovery) => discovery.Subscribe(keys =>
    {
        var relevant = false;
        foreach (var key in keys)
        {
            if (_manifest.Inject.Contains(key.Name, StringComparer.Ordinal))
            {
                relevant = true;
                break;
            }
        }

        if (!relevant)
        {
            return;
        }

        var satisfied = DependenciesSatisfied();
        var state = State;
        if (satisfied)
        {
            if (state == PluginLifecycleState.Pending && Volatile.Read(ref _rearmedPending) == 1)
            {
                // P2-13：依赖重现 → 自动重启（依赖消失卸载后的存活 PENDING）
                FireAndForget(StartAsync());
            }
            else if (state == PluginLifecycleState.Unloading)
            {
                // P1-4：卸载在途 → 先收敛再加入（修复前 StartCoreAsync 拒 Unloading → 未观察异常）
                FireAndForget(StartAfterUnloadSettlesAsync());
            }
            else if (state == PluginLifecycleState.Failed)
            {
                // P2-13：FAILED 随依赖变化重评（依赖超时后依赖到位 → 重启）
                FireAndForget(RestartIfFailedAsync());
            }
        }
        else if (state == PluginLifecycleState.Active)
        {
            // 依赖消失（值删）→ 依赖方走完整卸载闸门（落 PENDING，可 re-arm）
            FireAndForget(StopCoreAsync(rearmable: true));
        }
        else if (state == PluginLifecycleState.Loading)
        {
            // P1-5：加载期依赖消失 → 等加载收敛后卸载（对齐 fiber.ts:665-672：加载完成对比 epoch 再卸）
            FireAndForget(StopAfterLoadSettlesAsync());
        }
    });

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
    /// 返回的任务在到达 ACTIVE/FAILED 后完成。启动在途时并发调用幂等返回（P1-4 收口）。
    /// </summary>
    public async Task StartAsync()
    {
        if (Interlocked.Exchange(ref _startBusy, 1) == 1)
        {
            return; // 已有启动在途（rearm/显式并发）——状态经 State/AwaitAsync 观察
        }

        try
        {
            await StartCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _startBusy, 0);
        }
    }

    /// <summary>稳定等待（对齐 Cordis fiber.await）：ACTIVE/FAILED/DISPOSED 落定后完成；FAILED 重抛启动错误。
    /// P1-1（19 号审计 CF-2）：修复前 _settled 死字段——Pending/Loading 期等待立即返回。</summary>
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

    /// <summary>完整卸载闸门（quiesce，ADR-0005 决策 2）→ DISPOSED（终态）。显式调用 → 销毁依赖订阅。</summary>
    public async Task StopAsync()
    {
        await StopCoreAsync(rearmable: false).ConfigureAwait(false);
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
            await StopCoreAsync(rearmable: false).ConfigureAwait(false);
        }

        lock (_lock)
        {
            _error = null;
            _state = PluginLifecycleState.Pending;
            _settled = NewSettled();
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
        CancellationTokenSource lifecycle;
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
            _settled = NewSettled();
            _state = PluginLifecycleState.Pending;
            Volatile.Write(ref _rearmedPending, 0); // 启动即消费 rearm 标记
            _lifecycleCts = lifecycle = new CancellationTokenSource(); // P1-2：本次启动的取消源
        }

        SetState(PluginLifecycleState.Pending);
        if (!await AwaitDependenciesOrFailAsync(lifecycle).ConfigureAwait(false))
        {
            return; // 依赖超时 → 已置 FAILED；停止取消 → 静默返回（停方接管状态机）
        }

        SetState(PluginLifecycleState.Loading);
        try
        {
            await InitializePluginAsync().ConfigureAwait(false);

            if (lifecycle.IsCancellationRequested)
            {
                await CleanupCancelledStartAsync().ConfigureAwait(false);
                return;
            }

            SetState(PluginLifecycleState.Active);
            if (_context is ContextFacade facade)
            {
                facade.CommitProvidesStaging(); // D-7：ACTIVE 补发（暂存落库 + 单次合并通知）
            }

            await EmitLifecycleFactAsync(new Keystone.Runtime.Events.PluginStartedFact(_manifest.Id)).ConfigureAwait(false);
            CompleteSettled();
        }
        catch (Exception ex)
        {
            await TransitionToFailedAsync(ex).ConfigureAwait(false);
        }
    }

    /// <summary>初始化失败 → FAILED（持有错误 + 事实事件 + 落定通知）。</summary>
    private async Task TransitionToFailedAsync(Exception ex)
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

    /// <summary>P1-2：初始化期间被停止——弃暂存；停方可能已收敛（Disposed/Pending），未及收敛则补齐。</summary>
    private async Task CleanupCancelledStartAsync()
    {
        if (_context is ContextFacade cancelledFacade)
        {
            cancelledFacade.DiscardProvidesStaging();
        }

        if (State is not (PluginLifecycleState.Disposed or PluginLifecycleState.Pending))
        {
            await StopCoreAsync(rearmable: false).ConfigureAwait(false);
        }
    }

    /// <summary>LOADING 阶段：创建 context/插件实例 → 初始化（provide 暂存，D-7）→ 校验 provides 兑现（P2-16）。</summary>
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

        var facade = context as ContextFacade;
        // CA2000：暂存句柄刻意丢弃——提交/弃置由 Commit/DiscardProvidesStaging 显式控制
        //（StagingHandle.Dispose 即 Commit，此处不适用；泄漏防护 = DiscardStaging 幂等移除）
#pragma warning disable CA2000
        _ = facade?.BeginProvidesStaging();
#pragma warning restore CA2000
        try
        {
            await plugin.InitializeAsync(context, _config).ConfigureAwait(false);

            // 18 §2 CA-1 + P2-16（19 号审计 SV-7）：兑现 = 属主本人 Provide（他人同名同域值不得蒙混——
            // 修复前 IsAvailable 只查可用性）——消灭"门控放行但 Get 落空"静默晚失败
            var missing = _manifest.Provides
                .Where(service => facade is null
                    ? !_discovery.IsAvailable(service, Realm(service))
                    : !facade.HasProvided(service))
                .ToList();
            if (missing.Count > 0)
            {
                throw new KeystoneException(
                    ErrorCode.LifecycleLoadFailed,
                    $"plugin '{_manifest.Id}' declared provides [{string.Join(", ", missing)}] but did not Provide them during initialization");
            }
        }
        catch (Exception)
        {
            facade?.DiscardProvidesStaging(); // D-7：FAILED——暂存值从未可见，弃置
            throw;
        }
    }

    private bool DependenciesSatisfied()
        => _manifest.Inject.All(service => _discovery.IsAvailable(service, Realm(service)));

    /// <summary>门控域视图（18 §2 CA-1）：isolateMap 命中 → 该域；否则 ""（默认共享）。与 context 工厂注入的 map 同源等价。</summary>
    private string Realm(string serviceName)
        => _isolateMap is not null && _isolateMap.TryGetValue(serviceName, out var realm) ? realm : string.Empty;

    /// <summary>DC-5：等待依赖；超时 → FAILED（ADR-0007：不无限 PENDING）。
    /// P1-2：停止取消 → 静默 false（不翻 FAILED——停方接管状态机）。</summary>
    private async Task<bool> AwaitDependenciesOrFailAsync(CancellationTokenSource lifecycle)
    {
        try
        {
            await WaitForDependenciesAsync(lifecycle).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                if (lifecycle.IsCancellationRequested || _state is PluginLifecycleState.Unloading or PluginLifecycleState.Disposed)
                {
                    return false; // P1-2：停止已接管——延迟翻 FAILED 修复
                }

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

    /// <summary>事件驱动等待依赖（ADR-0007 决策 3：服务可用性事件 → 重新检查，非轮询）。
    /// P1-2：链接 lifecycle 取消源——停止即唤醒等待（不再坐等超时）。</summary>
    private async Task WaitForDependenciesAsync(CancellationTokenSource lifecycle)
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
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, lifecycle.Token);
        try
        {
            await tcs.Task.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (lifecycle.IsCancellationRequested)
        {
            subscription?.Dispose();
            throw new KeystoneException(
                ErrorCode.LifecycleInvalidState,
                $"plugin '{_manifest.Id}' start cancelled by stop");
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

    /// <summary>P1-3（19 号审计 CF-5）：卸载闸门互斥——并发停止经 _stopGate 串行化，
    /// 后到者见已落定状态直接返回（恰一次 quiesce/plugin dispose）。
    /// rearmable=true（依赖消失路径）→ 落 PENDING（P2-13 存活可 re-arm）；
    /// rearmable=false（显式停止）→ DISPOSED（终态）；终态停并入 rearm 停时补转移 PENDING → DISPOSED。</summary>
    private async Task StopCoreAsync(bool rearmable)
    {
        _lifecycleCts.Cancel(); // P1-2：取消在途依赖等待
        await _stopGate.WaitAsync().ConfigureAwait(false);
        try
        {
            PluginLifecycleState state;
            lock (_lock)
            {
                state = _state;
                if (state == PluginLifecycleState.Disposed)
                {
                    return; // 已终态
                }

                if (state == PluginLifecycleState.Pending && !rearmable && Volatile.Read(ref _rearmedPending) == 1)
                {
                    Volatile.Write(ref _rearmedPending, 0); // 终态停并入 rearm 停：补转移
                }
                else
                {
                    state = PluginLifecycleState.Unloading; // fallthrough 到 inner
                }
            }

            if (state != PluginLifecycleState.Unloading)
            {
                SetState(PluginLifecycleState.Disposed); // rearm-PENDING 的终态补转移（无残余可收敛）
                CompleteSettled();
                return;
            }

            await StopCoreInnerAsync(rearmable).ConfigureAwait(false);
        }
        finally
        {
            _stopGate.Release();
        }
    }

    private async Task StopCoreInnerAsync(bool rearmable)
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

        if (rearmable)
        {
            Volatile.Write(ref _rearmedPending, 1);
            SetState(PluginLifecycleState.Pending); // P2-13：存活可 re-arm（对齐 fiber.ts:611-623）
        }
        else
        {
            SetState(PluginLifecycleState.Disposed);
        }

        CompleteSettled();
    }

    /// <summary>P1-5：加载期依赖消失——等加载收敛（Active/Failed 落定）后卸载。</summary>
    private async Task StopAfterLoadSettlesAsync()
    {
        try
        {
            await AwaitAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // FAILED（初始化错误）——无需再停；依赖订阅保留，依赖到位将重评重启
        }

        if (!DependenciesSatisfied() && State == PluginLifecycleState.Active)
        {
            await StopCoreAsync(rearmable: true).ConfigureAwait(false);
        }
    }

    /// <summary>P1-4：卸载在途依赖重现——先并入在途卸载，落 PENDING 后（仍满足）再启动。</summary>
    private async Task StartAfterUnloadSettlesAsync()
    {
        await StopCoreAsync(rearmable: true).ConfigureAwait(false);
        if (DependenciesSatisfied() && State == PluginLifecycleState.Pending)
        {
            await StartAsync().ConfigureAwait(false);
        }
    }

    /// <summary>P2-13：FAILED 重评——依赖到位且仍 FAILED 时重启（竞态下状态已变则跳过）。</summary>
    private async Task RestartIfFailedAsync()
    {
        if (State == PluginLifecycleState.Failed)
        {
            await RestartAsync().ConfigureAwait(false);
        }
    }

    /// <summary>P1-4：rearm fire-and-forget——异常必须被观察（修复前 LifecycleInvalidState 未观察异常）。</summary>
    private static void FireAndForget(Task task)
    {
        // CA1031：rearm 旁路的兜底吞异常——状态经 State/AwaitAsync/事实事件观察
#pragma warning disable CA1031
        _ = ObserveAsync(task);
#pragma warning restore CA1031
        return;

        static async Task ObserveAsync(Task t)
        {
            try
            {
                await t.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // 观察 = 防未观察任务异常（状态机竞态下无效转移等）
            }
        }
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

    private static TaskCompletionSource NewSettled()
        => new(TaskCreationOptions.RunContinuationsAsynchronously); // P1-1：真等待（同步完成无内联风暴）

    private void CompleteSettled()
    {
        lock (_lock)
        {
            _settled?.TrySetResult();
        }
    }
}
