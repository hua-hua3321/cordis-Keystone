namespace Keystone.Runtime.Persistence;

/// <summary>
/// 事实事件存储契约（ADR-0009 决策 1）：append-only 事件日志。
/// 只追加不修改不删除（event-sourcing）；重放按查询过滤；保留策略 Prune。
/// 可插拔：默认本地文件实现；数据库/对象存储后续按需。
/// </summary>
public interface IEventStore : IAsyncDisposable
{
    /// <summary>追加事实事件（分配单调序号，返回该序号）。</summary>
    Task<long> AppendAsync(StoredFact fact, CancellationToken cancellationToken = default);

    /// <summary>重放（按查询过滤，IAsyncEnumerable 流式）。</summary>
    IAsyncEnumerable<StoredFact> ReplayAsync(ReplayQuery query, CancellationToken cancellationToken = default);

    /// <summary>当前最后序号（追加恢复点）。</summary>
    Task<long> GetLastSequenceAsync(CancellationToken cancellationToken = default);

    /// <summary>执行保留策略，返回移除条数。</summary>
    Task<int> PruneAsync(RetentionPolicy policy, CancellationToken cancellationToken = default);
}
