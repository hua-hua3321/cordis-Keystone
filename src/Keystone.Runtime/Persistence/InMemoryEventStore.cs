namespace Keystone.Runtime.Persistence;

/// <summary>内存事件存储（测试/轻量宿主）：append-only + 单调序号 + 查询过滤 + 保留策略。</summary>
public sealed class InMemoryEventStore : IEventStore
{
    private readonly List<StoredFact> _facts = [];
    private readonly Lock _lock = new();
    private long _sequence;

    public Task<long> AppendAsync(StoredFact fact, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fact);
        lock (_lock)
        {
            _sequence++;
            _facts.Add(fact with { Sequence = _sequence });
            return Task.FromResult(_sequence);
        }
    }

    public async IAsyncEnumerable<StoredFact> ReplayAsync(ReplayQuery query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        List<StoredFact> snapshot;
        lock (_lock)
        {
            snapshot = [.. _facts];
        }

        foreach (var fact in snapshot.Where(f => Match(query, f)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return fact;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public Task<long> GetLastSequenceAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_sequence);
        }
    }

    public Task<int> PruneAsync(RetentionPolicy policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        lock (_lock)
        {
            var cutoff = policy.Ttl is { } ttl ? DateTimeOffset.UtcNow - ttl : (DateTimeOffset?)null;
            var before = _facts.Count;
            _facts.RemoveAll(f =>
                (cutoff.HasValue && f.Timestamp < cutoff.Value)
                || (policy.MaxEvents is { } max && f.Sequence <= _sequence - max));
            return Task.FromResult(before - _facts.Count);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static bool Match(ReplayQuery query, StoredFact fact)
        => (!query.TaskId.HasValue || fact.TaskId == query.TaskId)
            && (query.Capability is null || string.Equals(fact.Capability, query.Capability, StringComparison.Ordinal))
            && fact.Sequence > query.AfterSequence
            && (!query.From.HasValue || fact.Timestamp >= query.From)
            && (!query.To.HasValue || fact.Timestamp <= query.To);
}
