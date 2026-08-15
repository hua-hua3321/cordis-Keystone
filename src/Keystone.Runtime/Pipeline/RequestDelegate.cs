using Keystone.Runtime.Context;

namespace Keystone.Runtime.Pipeline;

/// <summary>
/// 管道委托（ASP.NET Core 形状）：链中每个中间件包裹的"其余部分"。
/// 不调用即短路（waterfall 否决语义，ADR-0006）。
/// </summary>
public delegate Task RequestDelegate(IPluginContext context);
