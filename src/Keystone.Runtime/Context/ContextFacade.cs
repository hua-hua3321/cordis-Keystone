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
    private CancellationToken _requestCancellationToken; // DC-14：请求 CT 槽（actor 串行循环内设置——单写者无竞争）

    // 值层唯一事实源（18 §2 CA-1）：链上共享（子复用 root 的实例；独立 root 自持一份）
    private readonly KeyedServiceStore _store;
    // isolate map（名 → realm）：沿链继承（子含名 → 用子值 = 影子覆盖；均无 → "" 默认共享）
    private readonly IReadOnlyDictionary<string, string>? _isolateMap;
    // 本 context 提供的服务（名, realm, 删键 disposer）：RemoveOwnedServices 逐个 dispose（幂等）
    private readonly List<(string Name, string Realm, IDisposable Disposer)> _provides = [];
    private readonly Lock _providesLock = new(); // CA-2（P62）：watcher 线程重载 vs 插件线程 Provide 并发
    private readonly EventBus _events;
    private readonly EffectRegistry _effects = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly List<IContextInterceptor> _interceptors = [];
    private readonly string? _logCategoryPrefix;

    public ContextFacade(
        string name,
        IContext? parent = null,
        ILoggerFactory? loggerFactory = null,
        Keystone.Runtime.Persistence.IEventStore? eventStore = null,
        string? logCategoryPrefix = null,
        IReadOnlyDictionary<string, string>? isolateMap = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Parent = parent;
        _isolateMap = isolateMap;
        _store = (parent as ContextFacade)?._store ?? new KeyedServiceStore();
        // DC-20（05 §5 日志命名）：category = {能力域}/{插件 ID}——插件 context 继承 root 的域前缀
        _logCategoryPrefix = logCategoryPrefix
            ?? (parent as ContextFacade)?._logCategoryPrefix; // 子 context 继承（同一能力域）
        _loggerFactory = loggerFactory
            ?? (parent as ContextFacade)?._loggerFactory // 子 context 复用 root 的工厂
            ?? NullLoggerFactory.Instance;
        // 事件总线在 context 链间共享（子复用父的实例，对齐 Cordis 单事件系统 + 监听 filter，ID-08）
        // DC-11：新建总线携带事实持久化存储（有父总线时以父总线的 store 为准）
        _events = parent?.Events is EventBus parentBus ? parentBus : new EventBus(eventStore);
    }

    public string Name { get; }

    public IContext? Parent { get; }

    public IContext Root => Parent?.Root ?? this;

    public string BaseUrl { get; init; } = string.Empty;

    public IEventBus Events => _events;

    public KeyedServiceStore Services => _store;

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
    /// 服务解析（18 §2 CA-1）：按本链推导的 realm 查共享 KeyedServiceStore——
    /// 组合语义（默认共享域 ""）与隔离语义（isolate map 命中 → 私有/命名域）统一为键查。
    /// D-7：自读带属主——init 暂存期自读自暂存值（他人仍不可见）。
    /// </summary>
    private T? Resolve<T>(string serviceName)
        => _store.TryGet<T>(serviceName, ResolveRealm(serviceName), ownerId: Name);

    /// <summary>
    /// realm 沿链推导（对齐 Cordis ctx[symbols.isolate] 原型链查找）：自本 context 向上，
    /// 首个 isolate map 含该服务名的 facade 给出 realm；均无 → ""（默认共享）。
    /// </summary>
    private string ResolveRealm(string serviceName)
    {
        for (IContext? scope = this; scope is not null; scope = scope.Parent)
        {
            if (scope is ContextFacade facade
                && facade._isolateMap is { } map
                && map.TryGetValue(serviceName, out var realm))
            {
                return realm;
            }
        }

        return string.Empty;
    }

    public void Provide<T>(string serviceName, T instance)
    {
        NotifyWrite(serviceName, instance);

        // 18 §2 CA-1：值即注册——按本链 realm 写共享 store（组合 = 默认共享域；isolate = 私有/命名域）；
        // disposer 记入 _provides（G-C3 属主追踪），卸载时 dispose 即删键 + Removed 通知；
        // D-7：属主暂存激活时 Provide 自动走暂存（不可见）——Commit/InitStaging 由 PluginRuntime 控制
        var realm = ResolveRealm(serviceName);
        var disposer = _store.Provide(serviceName, realm, instance, ownerId: Name);
        lock (_providesLock)
        {
            _provides.Add((serviceName, realm, disposer));
        }
    }

    /// <summary>
    /// D-6（19 号审计 SV-1，对齐 reflect.ts:254-265 set）：原位更新本 context 已提供的服务值——
    /// 未提供抛错、不通知（依赖方门控不重评：换值 ≠ 下线/上线）；二次注册仍走 <see cref="Provide{T}"/> 报错式。
    /// </summary>
    public void Set<T>(string serviceName, T instance)
    {
        NotifyWrite(serviceName, instance);
        _store.Set(serviceName, ResolveRealm(serviceName), instance, ownerId: Name);
    }

    /// <summary>D-7（19 号审计 SV-2）：开启属主暂存——init 期 provide 延迟到 Commit（= ACTIVE 补发）。</summary>
    public IDisposable BeginProvidesStaging() => _store.BeginStaging(Name);

    /// <summary>D-7：提交暂存（ACTIVE 后调用——落库 + 单次合并通知）。</summary>
    public void CommitProvidesStaging() => _store.CommitStaging(Name);

    /// <summary>D-7：弃置暂存（FAILED——init 期提供的值从未可见）。</summary>
    public void DiscardProvidesStaging() => _store.DiscardStaging(Name);

    /// <summary>P2-16（19 号审计 SV-7）：本 context（属主）是否提供过该服务名（realm 按本链推导）。</summary>
    public bool HasProvided(string serviceName)
    {
        var realm = ResolveRealm(serviceName);
        lock (_providesLock)
        {
            return _provides.Any(p => string.Equals(p.Name, serviceName, StringComparison.Ordinal)
                && string.Equals(p.Realm, realm, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// G-C3 卸载钩子：注销本 context 属主提供的全部服务值（dispose 删键 disposer，幂等；
    /// 非属主/已移除均安全）。依赖方经发现层通知重评（T4 接线）。
    /// </summary>
    public void RemoveOwnedServices()
    {
        List<(string Name, string Realm, IDisposable Disposer)> snapshot;
        lock (_providesLock)
        {
            snapshot = [.. _provides]; // 快照迭代（Provide 并发安全）
            _provides.Clear();
        }

        foreach (var (_, _, disposer) in snapshot)
        {
            disposer.Dispose();
        }
    }

    // ── IPluginContext：事件订阅面（转发 Events；监听者 scope 缺省 = 本 context，G15）──

    // P0-6（19 号审计 CF-1/EV-5，对齐 Cordis events.ts:254-259——监听器即 fiber effect）：
    // 订阅同时挂 effect——context quiesce（DisposeEffectsAsync）自动退订，handler 不滞留
    // 共享总线（否则插件卸载后 ALC 被钉死）。手动 Dispose 退订与 quiesce 退订幂等共存。

    /// <summary>
    /// P2-29（19 号审计 EV-13，对齐 Cordis emit 的 fire-and-forget）：异步监听
    /// （<c>SubscribeParallel</c>）不阻塞发布方——立即返回，分发在后台进行（不 await 全部完成）；
    /// 异常被观察（不产生未观察任务异常，对齐 Cordis emit 不 await 返回 promise）。
    /// 需要等待/聚合错误用 <c>Events.PublishParallelAsync</c>；同步监听（Subscribe）经
    /// <c>Events.EmitAsync</c> 本就不阻塞。
    /// </summary>
    public void EmitFireAndForget<TEvent>(TEvent e)
    {
        _ = ObserveAsync(_events.PublishParallelAsync(e, this));
        return;

        static async Task ObserveAsync(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            // CA1031：后台分发的监听异常被观察后吞掉（发布方不感知——fire-and-forget 语义）
#pragma warning disable CA1031
            catch (Exception)
#pragma warning restore CA1031
            {
                // 观察 = 防未观察任务异常
            }
        }
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler, EventSubscriptionOptions? options = null)
        => TrackSubscription(_events.Subscribe(handler, Normalize(options)));

    public IDisposable SubscribeParallel<TEvent>(Func<TEvent, Task> handler, EventSubscriptionOptions? options = null)
        => TrackSubscription(_events.SubscribeParallel(handler, Normalize(options)));

    public IDisposable SubscribeSerial<TEvent>(Func<TEvent, Task<object?>> handler, EventSubscriptionOptions? options = null)
        => TrackSubscription(_events.SubscribeSerial(handler, Normalize(options)));

    public IDisposable SubscribeBail<TEvent>(Func<TEvent, object?> handler, EventSubscriptionOptions? options = null)
        => TrackSubscription(_events.SubscribeBail(handler, Normalize(options)));

    public IDisposable SubscribeWaterfall<TEvent>(WaterfallHandler<TEvent> handler, EventSubscriptionOptions? options = null)
        => TrackSubscription(_events.SubscribeWaterfall(handler, Normalize(options)));

    /// <summary>订阅挂 effect（quiesce 自动退订）；返回句柄手动 Dispose = 立即退订（幂等）。</summary>
    private IDisposable TrackSubscription(IDisposable subscription)
    {
        // CA2000：句柄刻意丢弃——不手动 Dispose（其语义是"执行 disposer"= 立即退订），
        // 退订由 quiesce（DisposeAllAsync）或订阅句柄手动 Dispose 触发，二者幂等
#pragma warning disable CA2000
        _ = _effects.Register(() =>
#pragma warning restore CA2000
        {
            subscription.Dispose();
            return Task.CompletedTask;
        }, label: "event-subscription");
        return subscription;
    }

    private EventSubscriptionOptions Normalize(EventSubscriptionOptions? options)
        => options is null
            ? new EventSubscriptionOptions { Scope = this }
            : options with { Scope = options.Scope ?? this };

    // ── IContext：Effect / 日志 / 拦截器 ──

    /// <summary>
    /// 当前请求取消令牌（DC-14，06 §1）：自身槽未设置时沿父链取（插件 handler 闭包读自身
    /// context 即得实例级请求 CT）；均无 = None（无请求语义）。
    /// </summary>
    public CancellationToken CancellationToken
        => _requestCancellationToken != default
            ? _requestCancellationToken
            : (Parent as ContextFacade)?.CancellationToken ?? default;

    /// <summary>
    /// 设置/复位请求 CT 槽（DC-14）：由运行时 actor 在串行消息循环内调用（请求开始设置/结束复位）；
    /// 嵌入方/插件只读——不手动调用（非线程安全面）。
    /// </summary>
    public void SetRequestCancellationToken(CancellationToken token) => _requestCancellationToken = token;

    /// <summary>命名日志（category：DC-20 = {域前缀}/{name}，无前缀 = name）。</summary>
    public ILogger GetLogger(string? name = null)
        => _loggerFactory.CreateLogger(
            _logCategoryPrefix is { } prefix ? $"{prefix}/{name ?? Name}" : name ?? Name);

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
