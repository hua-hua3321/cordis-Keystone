namespace Keystone.Runtime.Persistence;

/// <summary>保留策略（ADR-0009 决策 3）：TTL 或最大条数，Prune 时执行。</summary>
public sealed record RetentionPolicy(TimeSpan? Ttl = null, long? MaxEvents = null);
