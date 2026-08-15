using Keystone.Runtime.Context;

namespace Keystone.Runtime.Events;

/// <summary>
/// 事件总线（五分发模式，ADR-0006；订阅面按模式分方法，对齐 10-plugin-sdk §4）。
/// 订阅者经 <c>IContext.Events</c> 获取总线实例；发布时按 <see cref="EventSubscriptionOptions"/> 过滤（G15）。
/// </summary>
public interface IEventBus
{
    // ── 订阅面：按模式分方法（10-plugin-sdk §4）──

    /// <summary>emit 监听（同步 handler）。</summary>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler, EventSubscriptionOptions? options = null);

    /// <summary>parallel 监听（异步并发）。</summary>
    IDisposable SubscribeParallel<TEvent>(Func<TEvent, Task> handler, EventSubscriptionOptions? options = null);

    /// <summary>serial 监听（异步按序，返回首个非 null 决策值）。</summary>
    IDisposable SubscribeSerial<TEvent>(Func<TEvent, Task<object?>> handler, EventSubscriptionOptions? options = null);

    /// <summary>bail 监听（同步按序，返回首个非 null 决策值）。</summary>
    IDisposable SubscribeBail<TEvent>(Func<TEvent, object?> handler, EventSubscriptionOptions? options = null);

    /// <summary>waterfall 监听（包裹 next 链）。</summary>
    IDisposable SubscribeWaterfall<TEvent>(WaterfallHandler<TEvent> handler, EventSubscriptionOptions? options = null);

    // ── 发布面（publisher = 发布者 context，G15 过滤基准；纯总线场景可省略）──

    /// <summary>emit：按注册序调用，首错传播。</summary>
    Task EmitAsync<TEvent>(TEvent e, IContext? publisher = null, CancellationToken cancellationToken = default);

    /// <summary>parallel：并发执行 + 错误聚合。</summary>
    Task PublishParallelAsync<TEvent>(TEvent e, IContext? publisher = null, CancellationToken cancellationToken = default);

    /// <summary>serial：按序 await，首个非 null 返回值短路。</summary>
    Task<object?> PublishSerialAsync<TEvent>(TEvent e, IContext? publisher = null, CancellationToken cancellationToken = default);

    /// <summary>bail：同步按序，首个非 null 返回值短路。</summary>
    object? PublishBail<TEvent>(TEvent e, IContext? publisher = null);

    /// <summary>waterfall：包裹 next 链执行；监听者不调 next 即否决。</summary>
    Task PublishWaterfallAsync<TEvent>(TEvent e, IContext? publisher = null, CancellationToken cancellationToken = default);
}
