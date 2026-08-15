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
    private readonly IServiceRegistry _registry;
    private readonly Func<string, IPluginContext> _contextFactory;
    private readonly IReadOnlyDictionary<string, object?> _config;
    private readonly TimeSpan _quiesceTimeout;
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
        IServiceRegistry registry,
        Func<string, IPluginContext> contextFactory,
        IReadOnlyDictionary<string, object?>? config = null,
        TimeSpan? quiesceTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(pluginFactory);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(contextFactory);

        _manifest = manifest;
        _pluginFactory = pluginFactory;
        _registry = registry;
        _contextFactory = contextFactory;
        _config = config ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        _quiesceTimeout = quiesceTimeout ?? new KeystoneSettings().QuiesceTimeout;

        // 依赖消失 → 依赖方走完整卸载闸门（ADR-0007 决策 3）
        _dependencySubscription = registry.Subscribe(args =>
        {
            if (!args.Available && manifest.Inject.Contains(args.ServiceName, StringComparer.Ordinal)
                && State == PluginLifecycleState.Active)
            {
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

    /// <summary>完整卸载闸门（quiesce，ADR-0005 决策 2）→ DISPOSED。</summary>
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
    }

    private async Task StartCoreAsync()
    {
        lock (_lock)
        {
            if (_state is not (PluginLifecycleState.Pending or PluginLifecycleState.Failed))
            {
                throw new KeystoneException(
                    ErrorCode.LifecycleInvalidState,
                    $"cannot start plugin '{_manifest.Id}' from state '{_state}'");
            }
        }

        SetState(PluginLifecycleState.Pending);
        await WaitForDependenciesAsync().ConfigureAwait(false);

        SetState(PluginLifecycleState.Loading);
        try
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
            foreach (var service in _manifest.Provides)
            {
                _registry.Register(service, _manifest.Id);
            }

            SetState(PluginLifecycleState.Active);
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
            CompleteSettled();
        }
    }

    private bool DependenciesSatisfied()
        => _manifest.Inject.All(service => _registry.IsAvailable(service));

    /// <summary>事件驱动等待依赖（ADR-0007 决策 3：服务可用性事件 → 重新检查，非轮询）。</summary>
    private Task WaitForDependenciesAsync()
    {
        if (DependenciesSatisfied())
        {
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IDisposable? subscription = null;
        subscription = _registry.Subscribe(_ =>
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

        return tcs.Task;
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

        // ③ 摘除服务注册（internal/service 变更 → 依赖方重评）
        foreach (var service in _manifest.Provides)
        {
            _registry.Unregister(service, _manifest.Id);
        }

        _dependencySubscription?.Dispose();
        _dependencySubscription = null;
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
