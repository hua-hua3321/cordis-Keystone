using Keystone.Runtime.Context;

namespace Keystone.Runtime.Pipeline;

/// <summary>
/// 管道（中间件链 + 内置执行器终端）：一次 Invoke = 按 Order 顺序执行整条链。
/// Build 后不可变（原子替换语义，ADR-0003：换管道实例 = 换引用，旧管道在途排空后销毁）。
/// </summary>
public interface IPipeline
{
    /// <summary>以给定上下文执行整条管道链（直到短路或 terminal）。</summary>
    Task InvokeAsync(IPluginContext context);
}
