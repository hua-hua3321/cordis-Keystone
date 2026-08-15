using MessagePack;

namespace Keystone.Runtime.Persistence;

/// <summary>
/// 事实事件记录（ADR-0009 决策 1）：append-only，只追加不修改。
/// <see cref="SchemaVersion"/> = 事件格式版本（迁移策略，ADR-0009 风险表）；
/// <see cref="Durable"/> = 分级标记（DC-18，ADR-0009 决策 3：true = 必须存活；
/// 旧格式数据缺该键 → false 尽力写语义）。
/// </summary>
[MessagePackObject]
public sealed record StoredFact
{
    [Key(0)]
    public int SchemaVersion { get; init; }

    [Key(1)]
    public long Sequence { get; init; }

    [Key(2)]
    public Guid FactId { get; init; }

    [Key(3)]
    public string? EventName { get; init; }

    [Key(4)]
    public Guid TaskId { get; init; }

    [Key(5)]
    public string? Capability { get; init; }

    [Key(6)]
    public byte[]? PayloadBytes { get; init; }

    [Key(7)]
    public DateTimeOffset Timestamp { get; init; }

    [Key(8)]
    public bool Durable { get; init; }
}
