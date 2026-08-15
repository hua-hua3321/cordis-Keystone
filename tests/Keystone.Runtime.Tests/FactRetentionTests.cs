using Keystone.Runtime.Persistence;

namespace Keystone.Runtime.Tests;

/// <summary>
/// DC-18（17-doc-compliance-audit / ADR-0009 决策 3）：事件分级落盘 + Prune 归档 + 定时执行。
/// 修复前：StoredFact 无 Durable（重放方无法区分必须存活/尽力写）；Prune 直接删除无归档；
/// PruneAsync 只能手动调用（无定时）。
/// 兑现：StoredFact.Durable 记录分级；Prune 被清事实先归档（同帧格式可重放）；
/// FactRetentionScheduler 周期执行（失败降级不崩宿主）。
/// </summary>
public class FactRetentionTests
{
    private static string TempPath(string suffix)
        => Path.Combine(Path.GetTempPath(), $"keystone-ret-{Guid.NewGuid():N}{suffix}");

    private static StoredFact Fact(long seq, bool durable = false, int ageDays = 0)
        => new()
        {
            SchemaVersion = 1,
            Sequence = seq,
            FactId = Guid.NewGuid(),
            EventName = durable ? "DurableFact" : "BestEffortFact",
            TaskId = Guid.NewGuid(),
            Capability = "fs",
            PayloadBytes = [],
            Timestamp = DateTimeOffset.UtcNow - TimeSpan.FromDays(ageDays),
            Durable = durable,
        };

    private static async Task<List<StoredFact>> ReadAll(string path)
    {
        var facts = new List<StoredFact>();
        await using var store = new FileEventStore(path);
        await foreach (var f in store.ReplayAsync(new ReplayQuery()))
        {
            facts.Add(f);
        }

        return facts;
    }

    [Fact]
    public async Task Durable_flag_round_trips_through_store()
    {
        var path = TempPath(".yml.facts");
        try
        {
            await using (var store = new FileEventStore(path))
            {
                await store.AppendAsync(Fact(1, durable: false));
                await store.AppendAsync(Fact(2, durable: true));
            }

            var facts = await ReadAll(path);
            Assert.False(facts[0].Durable); // 分级随事实落盘（重放方可区分）
            Assert.True(facts[1].Durable);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Prune_archives_removed_facts_before_dropping()
    {
        var path = TempPath(".yml.facts");
        var archive = TempPath(".archive.facts");
        try
        {
            await using (var store = new FileEventStore(path, archivePath: archive))
            {
                for (var i = 1; i <= 5; i++)
                {
                    await store.AppendAsync(Fact(i, durable: i % 2 == 0));
                }

                var removed = await store.PruneAsync(new RetentionPolicy(MaxEvents: 2));
                Assert.Equal(3, removed); // 保留最近 2 条（seq 4/5），清 3 条（seq 1/2/3）
            }

            var kept = await ReadAll(path);
            Assert.Equal([4, 5], kept.Select(f => f.Sequence));

            var archived = await ReadAll(archive); // 归档同帧格式——可重放
            Assert.Equal([1, 2, 3], archived.Select(f => f.Sequence));
            Assert.Equal([false, true, false], archived.Select(f => f.Durable)); // 分级随归档保留
        }
        finally
        {
            File.Delete(path);
            File.Delete(archive);
        }
    }

    [Fact]
    public async Task Prune_without_archive_path_keeps_delete_behavior()
    {
        var path = TempPath(".yml.facts");
        try
        {
            await using (var store = new FileEventStore(path))
            {
                await store.AppendAsync(Fact(1));
                await store.AppendAsync(Fact(2));
                await store.PruneAsync(new RetentionPolicy(MaxEvents: 1));
            }

            var kept = await ReadAll(path);
            Assert.Equal([2], kept.Select(f => f.Sequence)); // 未配置归档 → 纯删除（原行为）
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class SpyStore : IEventStore
    {
        public int PruneCalls;

        public Task<long> AppendAsync(StoredFact fact, CancellationToken cancellationToken = default)
            => Task.FromResult(0L);

        public Task<long> GetLastSequenceAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0L);

        public IAsyncEnumerable<StoredFact> ReplayAsync(ReplayQuery query, CancellationToken cancellationToken = default)
            => ReplayCore(cancellationToken);

        private static async IAsyncEnumerable<StoredFact> ReplayCore(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            yield break;
        }

        public Task<int> PruneAsync(RetentionPolicy policy, CancellationToken cancellationToken = default)
        {
            PruneCalls++;
            return Task.FromResult(0);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Scheduler_executes_prune_periodically()
    {
        var store = new SpyStore();
        using var scheduler = new FactRetentionScheduler(
            store, new RetentionPolicy(MaxEvents: 10), TimeSpan.FromMilliseconds(20));
        scheduler.Start();

        var deadline = Task.Delay(TimeSpan.FromSeconds(5));
        while (store.PruneCalls < 3 && !deadline.IsCompleted)
        {
            await Task.Delay(20);
        }

        Assert.True(store.PruneCalls >= 3); // 周期执行（20ms 间隔 × 3 次）
    }

    [Fact]
    public async Task Scheduler_swallows_prune_failures()
    {
        var store = new ThrowingStore();
        using var scheduler = new FactRetentionScheduler(
            store, new RetentionPolicy(MaxEvents: 10), TimeSpan.FromMilliseconds(10));
        scheduler.Start();

        await Task.Delay(100); // 失败降级：循环存活不抛（ADR-0009 不阻塞主链路硬约束）
        Assert.True(store.Calls >= 1);
    }

    private sealed class ThrowingStore : IEventStore
    {
        public int Calls;

        public Task<long> AppendAsync(StoredFact fact, CancellationToken cancellationToken = default)
            => throw new IOException("disk full");

        public Task<long> GetLastSequenceAsync(CancellationToken cancellationToken = default)
            => throw new IOException("disk full");

        public IAsyncEnumerable<StoredFact> ReplayAsync(ReplayQuery query, CancellationToken cancellationToken = default)
            => throw new IOException("disk full");

        public Task<int> PruneAsync(RetentionPolicy policy, CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new IOException("disk full");
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
