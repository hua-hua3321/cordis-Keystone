namespace Keystone.Runtime.Events;

/// <summary>
/// 事实事件标记（03 §4 事件分层 / ADR-0009）：实现此接口的事件在 <b>emit 分发</b>时
/// 持久化到事件存储（append-only 事件日志）——"任务完成/失败必须存活"。
/// <see cref="Durable"/> = true 时写失败向上传播（必须落盘）；false = 尽力写（失败降级，ADR-0009 决策 3）。
/// 拦截事件（waterfall）/策略事件（parallel/emit 观察者）不实现此接口——不持久。
/// </summary>
public interface IFactEvent
{
    /// <summary>关联任务（StoredFact.TaskId；无任务关联的事实用 <see cref="Guid.Empty"/>）。</summary>
    Guid TaskId { get; }

    /// <summary>能力域（实例名；非能力域事实为 null）。</summary>
    string? Capability { get; }

    /// <summary>已序列化载荷（可选；null = 无载荷）。</summary>
    byte[]? Payload { get; }

    /// <summary>true = 必须落盘（写失败传播）；false = 尽力写（失败降级）。</summary>
    bool Durable { get; }
}
