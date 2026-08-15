namespace Keystone.Runtime.Persistence;

/// <summary>重放查询（ADR-0009 决策 2）：TaskId / 能力域 / 时间范围 / 起始序号。</summary>
public sealed record ReplayQuery(
    Guid? TaskId = null,
    string? Capability = null,
    long AfterSequence = 0,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);
