using System.Runtime.CompilerServices;
using Keystone.Runtime.Effects;
using Keystone.Runtime.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Keystone.Runtime.Context;

/// <summary>
/// 上下文门面实现：组合事件总线、服务存储（属主校验）、Effect 注册表、日志工厂、拦截器链。
/// 服务访问经拦截器通知（H3）；Get/Provide 带属主 = 本 context 名（P2 简化，插件身份 P3 细化）。
/// </summary>
public sealed class ContextFacade : IPluginContext, IContext
{
    private readonly ServiceStore _services = new();
    private readonly EventBus _events;
    private readonly EffectRegistry _effects = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly List<IContextInterceptor> _interceptors = [];
    private readonly HashSet<string> _ownedServices = new(StringComparer.Ordinal);

    public ContextFacade(string name, IContext? parent = null, ILoggerFactory? loggerFactory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Parent = parent;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        // 事件总线在 context 链间共享（子复用父的实例，对齐 Cordis 单事件系统 + 监听 filter，ID-08）
        _events = parent?.Events is EventBus parentBus ? parentBus : new EventBus();
    }

    public string Name { get; }

    public IContext? Parent { get; }

    public IContext Root => Parent?.Root ?? this;

    public string BaseUrl { get; init; } = string.Empty;

    public IEventBus Events => _events;

    public IServiceStore Services => _services;

    public IContext Context => this;

    public ILogger Logger => GetLogger(Name);

    // ── IPluginContext：服务面（属主 = 本 context 名）──

    public T Get<T>(string serviceName)
    {
        NotifyRead(serviceName);
        return Resolve<T>(serviceName)
            ?? throw new Keystone.Core.Errors.KeystoneException(
                Keystone.Core.Errors.ErrorCode.GatingServiceNotFound,
                $"service '{serviceName}' is not provided in scope chain of '{Name}'");
    }

    public T? TryGet<T>(string serviceName)
    {
        NotifyRead(serviceName);
        return Resolve<T>(serviceName);
    }

    /// <summary>G-C5/M4 方法级延迟注入：首次访问 .Value 才解析（对齐 Cordis @Inject 方法级）。</summary>
    public Lazy<Task<T>> GetLazy<T>(string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        return new Lazy<Task<T>>(() => Task.FromResult(Get<T>(serviceName)));
    }

    /// <summary>
    /// 服务解析链（03 §2 作用域链 / ADR-0007 依赖门控）：先查本 scope，再沿父链向上（→ 根）。
    /// 父链 = 服务级组合（inject 跨插件可见）；isolate 隔离经独立 context 链天然达成（不共享父）。
    /// </summary>
    private T? Resolve<T>(string serviceName)
    {
        for (IContext? scope = this; scope is not null; scope = scope.Parent)
        {
            if (scope is ContextFacade facade && facade._services.TryGet<T>(serviceName) is { } found)
            {
                return found;
            }
        }

        return default;
    }

    public void Provide<T>(string serviceName, T instance)
    {
        NotifyWrite(serviceName, instance);

        // 03 §2.1 组合语义：插件（子 scope）服务注册到公共祖先（root），兄弟插件经父链可见；
        // 隔离实例（独立 root / isolate）服务留在本地，互不可见（03 §2.2）。
        _ownedServices.Add(serviceName); // G-C3：属主追踪（卸载时注销）
        if (Parent is not null && Root is ContextFacade root)
        {
            root._services.Set(serviceName, instance, ownerId: Name);
            return;
        }

        _services.Set(serviceName, instance, ownerId: Name);
    }

    /// <summary>
    /// G-C3 卸载钩子：注销本 context 属主提供的全部服务值（root/本地 store）。
    /// 依赖方经 registry 事件重评（manifest 名由 PluginRuntime.StopCoreAsync 的 Unregister 处理）。
    /// </summary>
    public void RemoveOwnedServices()
    {
        foreach (var serviceName in _ownedServices)
        {
            if (Parent is not null && Root is ContextFacade root)
            {
                root._services.Remove(serviceName, ownerId: Name);
            }
            else
            {
                _services.Remove(serviceName, ownerId: Name);
            }
        }

        _ownedServices.Clear();
    }

    // ── IPluginContext：事件订阅面（转发 Events；监听者 scope 缺省 = 本 context，G15）──

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler, EventSubscriptionOptions? options = null)
        => _events.Subscribe(handler, Normalize(options));

    public IDisposable SubscribeParallel<TEvent>(Func<TEvent, Task> handler, EventSubscriptionOptions? options = null)
        => _events.SubscribeParallel(handler, Normalize(options));

    public IDisposable SubscribeSerial<TEvent>(Func<TEvent, Task<object?>> handler, EventSubscriptionOptions? options = null)
        => _events.SubscribeSerial(handler, Normalize(options));

    public IDisposable SubscribeBail<TEvent>(Func<TEvent, object?> handler, EventSubscriptionOptions? options = null)
        => _events.SubscribeBail(handler, Normalize(options));

    public IDisposable SubscribeWaterfall<TEvent>(WaterfallHandler<TEvent> handler, EventSubscriptionOptions? options = null)
        => _events.SubscribeWaterfall(handler, Normalize(options));

    private EventSubscriptionOptions Normalize(EventSubscriptionOptions? options)
        => options is null
            ? new EventSubscriptionOptions { Scope = this }
            : options with { Scope = options.Scope ?? this };

    // ── IContext：Effect / 日志 / 拦截器 ──

    public ILogger GetLogger(string? name = null) => _loggerFactory.CreateLogger(name ?? Name);

    public IDisposable Effect(Func<Task> disposer, string? label = null, [CallerMemberName] string? callerMember = null)
        => _effects.Register(disposer, label, callerMember);

    public IReadOnlyList<EffectMeta> GetEffects() => _effects.GetEffects();

    public Task DisposeEffectsAsync() => _effects.DisposeAllAsync();

    public void AddInterceptor(IContextInterceptor interceptor)
    {
        ArgumentNullException.ThrowIfNull(interceptor);
        _interceptors.Add(interceptor);
    }

    public void RemoveInterceptor(IContextInterceptor interceptor) => _interceptors.Remove(interceptor);

    private void NotifyRead(string serviceName)
    {
        foreach (var interceptor in _interceptors)
        {
            interceptor.OnServiceReadAsync(serviceName, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }
    }

    private void NotifyWrite(string serviceName, object? value)
    {
        foreach (var interceptor in _interceptors)
        {
            interceptor.OnServiceWriteAsync(serviceName, value, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }
    }
}
