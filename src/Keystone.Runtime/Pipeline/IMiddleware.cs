using Keystone.Runtime.Context;

namespace Keystone.Runtime.Pipeline;

/// <summary>
/// 中间件接口（形状 A，04 §2/§4 定案）：插件 SDK 公开面。
/// <see cref="Id"/> = 插件 ID（诊断）；<see cref="Order"/> = 管道顺序；
/// InvokeAsync 中不调 next 即短路。
/// </summary>
public interface IMiddleware
{
    /// <summary>插件 ID（诊断/日志）。</summary>
    string Id { get; }

    /// <summary>管道顺序（升序执行；相同 Order 保持注册序）。</summary>
    int Order { get; }

    /// <summary>执行中间件；await next 之后的代码即 after 语义。</summary>
    Task InvokeAsync(IPluginContext ctx, RequestDelegate next);
}
