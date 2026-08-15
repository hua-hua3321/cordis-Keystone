using Keystone.Runtime.Events;
using Microsoft.Extensions.Logging;

namespace Keystone.Runtime.Context;

/// <summary>
/// 插件侧门面（10-plugin-sdk §4）：服务解析/注册（属主 = 本插件）、五模式事件订阅、日志。
/// 计时器接口（SetTimeout 等）随插件生命周期回收，P3 补齐（依赖 fiber 语义）。
/// </summary>
public interface IPluginContext
{
    /// <summary>底层运行时上下文（完整面）。</summary>
    IContext Context { get; }

    /// <summary>
    /// 当前请求的取消令牌（DC-14，06 §1：取消贯穿全链）：中间件/handler 经 context 读取；
    /// 无请求语义（初始化/后台）= <see cref="CancellationToken.None"/>；
    /// 请求 CT 沿 context 链向上取实例级槽位（插件 handler 闭包读自身 context 即得）。
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>按服务名解析；未就绪 → PENDING 等待后注入（ADR-0007，P3 实现）。</summary>
    T Get<T>(string serviceName);

    /// <summary>按服务名可选解析。</summary>
    T? TryGet<T>(string serviceName);

    /// <summary>
    /// 方法级延迟注入（G-C5/M4，对齐 Cordis @Inject 方法级，registry.ts:45-59）：
    /// 返回 <see cref="Lazy{T}"/>——首次访问 .Value 才解析（服务不可用则抛 GatingServiceNotFound）。
    /// 用途：初始化时不解析、方法执行时服务已就绪（避免初始化期依赖未注入）。
    /// </summary>
    Lazy<Task<T>> GetLazy<T>(string serviceName);

    /// <summary>提供服务（属主 = 本插件；rebind/属主校验，03 §2.1/§2.3）。</summary>
    void Provide<T>(string serviceName, T instance);

    /// <summary>emit 监听。</summary>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler, EventSubscriptionOptions? options = null);

    /// <summary>parallel 监听。</summary>
    IDisposable SubscribeParallel<TEvent>(Func<TEvent, Task> handler, EventSubscriptionOptions? options = null);

    /// <summary>serial 监听。</summary>
    IDisposable SubscribeSerial<TEvent>(Func<TEvent, Task<object?>> handler, EventSubscriptionOptions? options = null);

    /// <summary>bail 监听。</summary>
    IDisposable SubscribeBail<TEvent>(Func<TEvent, object?> handler, EventSubscriptionOptions? options = null);

    /// <summary>waterfall 监听。</summary>
    IDisposable SubscribeWaterfall<TEvent>(WaterfallHandler<TEvent> handler, EventSubscriptionOptions? options = null);

    /// <summary>命名日志（category = 插件 ID）。</summary>
    ILogger Logger { get; }
}
