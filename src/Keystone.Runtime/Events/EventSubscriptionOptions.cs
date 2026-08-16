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

    /// <summary>
    /// D-8（19 号审计 EV-1，对齐 events.ts:159-176）：缺省 false = 广播（跨 scope 可收——
    /// Cordis ctx.on 注册的监听不带 filter 即全收）；显式 true = 祖先链过滤
    /// （监听者须是发布者的祖先/自身才投递——等价 Cordis internal/service 显式携带 isolate filter）。
    /// </summary>
    public bool ScopeFilter { get; init; }

    /// <summary>true = 注册到监听列表头部（prepend，M7）。</summary>
    public bool Prepend { get; init; }

    /// <summary>true = 触发一次后自动退订（M7 once）。</summary>
    public bool Once { get; init; }
}
