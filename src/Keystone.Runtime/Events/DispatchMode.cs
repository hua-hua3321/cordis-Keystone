namespace Keystone.Runtime.Events;

/// <summary>
/// 事件分发模式全集（ADR-0006）：emit/parallel/serial/bail/waterfall。
/// </summary>
public enum DispatchMode
{
    /// <summary>fire-and-forget：按注册序调用，忽略返回值（首错传播，对齐 Cordis 同步 emit）。</summary>
    Emit = 0,

    /// <summary>并发执行，全部完成；错误聚合（Task.WhenAll）。</summary>
    Parallel = 1,

    /// <summary>按序 await，首个非 null 返回值短路。</summary>
    Serial = 2,

    /// <summary>同步按序，首个非空返回值短路。</summary>
    Bail = 3,

    /// <summary>包裹 next 链（中间件形状），不调 next 即否决。</summary>
    Waterfall = 4,
}
