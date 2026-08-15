using Keystone.Runtime.Context;

namespace Keystone.Runtime.Events;

/// <summary>
/// 订阅选项：<see cref="Prepend"/>（M7 监听顺序）、<see cref="Once"/>（M7 只触发一次）、
/// <see cref="Scope"/> / <see cref="Global"/>（G15 事件过滤）。
/// </summary>
public sealed record EventSubscriptionOptions
{
    /// <summary>监听者所属 context（G15 过滤基准）；缺省 = 订阅时所在 context。</summary>
    public IContext? Scope { get; init; }

    /// <summary>true = 跳过 scope 过滤，任何分发都投递（对齐 Cordis global: true）。</summary>
    public bool Global { get; init; }

    /// <summary>true = 注册到监听列表头部（prepend，M7）。</summary>
    public bool Prepend { get; init; }

    /// <summary>true = 触发一次后自动退订（M7 once）。</summary>
    public bool Once { get; init; }
}
