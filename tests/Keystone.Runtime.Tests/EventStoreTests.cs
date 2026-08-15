using Keystone.Runtime.Persistence;
using Keystone.Core.Contracts;
using MessagePack;

namespace Keystone.Runtime.Tests;

public class InMemoryEventStoreTests
{
    [Fact]
    public async Task Append_assigns_monotonic_sequence()
    {
        var store = new InMemoryEventStore();

        var s1 = await store.AppendAsync(Fact("evt1"));
        var s2 = await store.AppendAsync(Fact("evt2"));

        Assert.Equal(1, s1);
        Assert.Equal(2, s2);
        Assert.Equal(2, await store.GetLastSequenceAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Replay_returns_events_in_order_and_filters()
    {
        var store = new InMemoryEventStore();
        var taskId = TaskId.New();
        await store.AppendAsync(Fact("evt1", taskId: taskId, capability: "fs"));
        await store.AppendAsync(Fact("evt2", taskId: taskId, capability: "fs"));
        await store.AppendAsync(Fact("other", taskId: TaskId.New(), capability: "llm"));

        var replayed = new List<StoredFact>();
        await foreach (var fact in store.ReplayAsync(new ReplayQuery(TaskId: taskId.Value), CancellationToken.None))
        {
            replayed.Add(fact);
        }

        Assert.Equal(2, replayed.Count);
        Assert.All(replayed, f => Assert.Equal(taskId.Value, f.TaskId));
    }

    [Fact]
    public async Task Replay_after_sequence_skips_earlier_events()
    {
        var store = new InMemoryEventStore();
        await store.AppendAsync(Fact("a"));
        await store.AppendAsync(Fact("b"));
        await store.AppendAsync(Fact("c"));

        var replayed = new List<StoredFact>();
        await foreach (var fact in store.ReplayAsync(new ReplayQuery(AfterSequence: 1), CancellationToken.None)) // 排除 a（序号 1），返回 b/c
        {
            replayed.Add(fact);
        }

        Assert.Equal(["b", "c"], replayed.Select(f => f.EventName));
    }

    [Fact]
    public async Task Prune_removes_expired_events()
    {
        var store = new InMemoryEventStore();
        await store.AppendAsync(Fact("old", timestamp: DateTimeOffset.UtcNow.AddHours(-2)));
        await store.AppendAsync(Fact("new", timestamp: DateTimeOffset.UtcNow));

        var pruned = await store.PruneAsync(new RetentionPolicy(Ttl: TimeSpan.FromHours(1)));

        Assert.Equal(1, pruned);
        var remaining = new List<StoredFact>();
        await foreach (var fact in store.ReplayAsync(new ReplayQuery(), CancellationToken.None))
        {
            remaining.Add(fact);
        }

        Assert.Single(remaining);
        Assert.Equal("new", remaining[0].EventName);
    }

    internal static StoredFact Fact(
        string eventName,
        TaskId? taskId = null,
        string capability = "fs",
        DateTimeOffset? timestamp = null)
        => new()
        {
            FactId = Guid.NewGuid(),
            EventName = eventName,
            TaskId = taskId?.Value ?? Guid.NewGuid(),
            Capability = capability,
            PayloadBytes = MessagePackSerializer.Serialize(new Dictionary<string, object?> { ["op"] = eventName }),
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
        };
}

public class FileEventStoreTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("keystone-events-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Append_then_reopen_preserves_order()
    {
        var path = Path.Combine(_directory, "events.bin");
        long last;
        await using (var store = new FileEventStore(path))
        {
            await store.AppendAsync(InMemoryEventStoreTests.Fact("a"));
            await store.AppendAsync(InMemoryEventStoreTests.Fact("b"));
            last = await store.GetLastSequenceAsync(CancellationToken.None);
        }

        Assert.Equal(2, last);

        await using var reopened = new FileEventStore(path);
        var replayed = new List<StoredFact>();
        await foreach (var fact in reopened.ReplayAsync(new ReplayQuery(), CancellationToken.None))
        {
            replayed.Add(fact);
        }

        Assert.Equal(["a", "b"], replayed.Select(f => f.EventName));
        Assert.Equal([1L, 2L], replayed.Select(f => f.Sequence));
    }

    [Fact]
    public async Task Crash_recovery_reads_complete_prefix()
    {
        // 崩溃恢复：文件尾部存在损坏帧 → 读到完整前缀帧，顺序一致（ADR-0009 风险缓解）
        var path = Path.Combine(_directory, "events.bin");
        await using (var store = new FileEventStore(path))
        {
            await store.AppendAsync(InMemoryEventStoreTests.Fact("a"));
            await store.AppendAsync(InMemoryEventStoreTests.Fact("b"));
        }

        // 模拟崩溃：追加垃圾字节（损坏尾部）
        await File.AppendAllBytesAsync(path, [0xFF, 0xFF, 0x01, 0x02]);

        await using var recovered = new FileEventStore(path);
        var replayed = new List<StoredFact>();
        await foreach (var fact in recovered.ReplayAsync(new ReplayQuery(), CancellationToken.None))
        {
            replayed.Add(fact);
        }

        Assert.Equal(["a", "b"], replayed.Select(f => f.EventName)); // 完整前缀恢复
    }

    [Fact]
    public async Task Concurrent_appends_are_serialized()
    {
        var path = Path.Combine(_directory, "events.bin");
        await using var store = new FileEventStore(path);

        await Task.WhenAll(Enumerable.Range(0, 10).Select(i => store.AppendAsync(InMemoryEventStoreTests.Fact($"e{i}"))));

        Assert.Equal(10, await store.GetLastSequenceAsync(CancellationToken.None));
    }
}

public class EventMigratorTests
{
    [Fact]
    public void Migrates_old_schema_version_to_latest()
    {
        var migrator = new EventMigrator(new Dictionary<int, Func<StoredFact, StoredFact>>
        {
            [1] = fact => fact with { SchemaVersion = 2, PayloadBytes = [9, 9, 9] }, // v1 → v2：payload 转换
        });

        var v1 = InMemoryEventStoreTests.Fact("evt") with { SchemaVersion = 1 };
        var migrated = migrator.Migrate(v1);

        Assert.Equal(2, migrated.SchemaVersion);
        Assert.Equal([9, 9, 9], migrated.PayloadBytes);
    }

    [Fact]
    public void Current_version_passes_through()
    {
        var migrator = new EventMigrator(new Dictionary<int, Func<StoredFact, StoredFact>>
        {
            [1] = fact => fact with { SchemaVersion = 2 },
        });

        var current = InMemoryEventStoreTests.Fact("evt") with { SchemaVersion = 2 };

        Assert.Equal(2, migrator.Migrate(current).SchemaVersion);
    }
}
