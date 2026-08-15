using Keystone.Core.Contracts;
using Keystone.Runtime.Actors;
using Keystone.Runtime.Events;
using Keystone.Runtime.Persistence;
using Proto;

namespace Keystone.Runtime.Tests;

/// <summary>
/// DC-11（17-doc-compliance-audit）：事实事件写入 IEventStore（ADR-0009/03 §4）。
/// 修复前：IEventStore 孤立，EventBus/PluginRuntime 不写存储——"任务完成/失败必须存活"未兑现。
/// 接线：IFactEvent 标记（emit 分发时持久化）+ 能力域任务事实 + 插件生命周期事实。
/// </summary>
public class FactPersistenceTests
{
    private sealed record Ping(string Value)
    {
        public Guid TaskId => Guid.Empty;
    }

    private sealed record TaskDone(Guid TaskId, string Capability) : IFactEvent
    {
        public byte[]? Payload => null;

        public bool Durable => false;
    }

    private sealed class FailingStore : IEventStore
    {
        public Task<long> AppendAsync(StoredFact fact, CancellationToken cancellationToken = default)
            => throw new IOException("disk full");

        public IAsyncEnumerable<StoredFact> ReplayAsync(ReplayQuery query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<long> GetLastSequenceAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0L);

        public Task<int> PruneAsync(RetentionPolicy policy, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Emitting_fact_event_appends_to_store()
    {
        var store = new InMemoryEventStore();
        var bus = new EventBus(store);
        var taskId = Guid.NewGuid();

        await bus.EmitAsync(new TaskDone(taskId, "fs"));

        Assert.Equal(1, await store.GetLastSequenceAsync(CancellationToken.None));
        var replayed = new List<StoredFact>();
        await foreach (var fact in store.ReplayAsync(new ReplayQuery(TaskId: taskId), CancellationToken.None))
        {
            replayed.Add(fact);
        }

        var persisted = Assert.Single(replayed);
        Assert.Equal("TaskDone", persisted.EventName);
        Assert.Equal(taskId, persisted.TaskId);
        Assert.Equal("fs", persisted.Capability);
    }

    [Fact]
    public async Task Non_fact_events_are_not_persisted()
    {
        var store = new InMemoryEventStore();
        var bus = new EventBus(store);

        await bus.EmitAsync(new Ping("x"));

        Assert.Equal(0, await store.GetLastSequenceAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Non_durable_fact_append_failure_does_not_break_emit()
    {
        // ADR-0009 决策 3：默认尽力写，失败降级不影响主链路
        var bus = new EventBus(new FailingStore());

        var handlerCalled = false;
        bus.Subscribe<TaskDone>(_ => handlerCalled = true);

        await bus.EmitAsync(new TaskDone(Guid.NewGuid(), "fs")); // 不抛

        Assert.True(handlerCalled); // 观察者照常收到
    }

    private sealed record DurableFact(Guid TaskId) : IFactEvent
    {
        public string? Capability => null;

        public byte[]? Payload => null;

        public bool Durable => true;
    }

    [Fact]
    public async Task Durable_fact_append_failure_propagates()
    {
        // ADR-0009 决策 3：durable: true 写失败必须暴露（任务标记失败/告警）
        var bus = new EventBus(new FailingStore());

        await Assert.ThrowsAsync<IOException>(() => bus.EmitAsync(new DurableFact(Guid.NewGuid())));
    }

    [Fact]
    public async Task Capability_actor_persists_task_completion_facts()
    {
        // 04 §7：任务完成/失败 = 事实事件（必须存活）——能力域 actor 在处理后发布
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var system = new ActorSystem();
        var domain = CapabilityDomain.Attach(system, "fs");
        var store = new InMemoryEventStore();
        var taskId = Guid.NewGuid();

        var handle = domain.Spawn(
            "fs-a",
            envelope => Task.FromResult(new TaskResultEnvelope
            {
                TaskId = envelope.TaskId,
                Succeeded = true,
                Type = TaskResultType.Completed,
            }),
            eventStore: store);

        var result = await domain.RequestAsync(handle, new TaskEnvelope
        {
            TaskId = taskId,
            Capability = "fs",
            Operation = "read",
            PayloadBytes = [],
        }, cts.Token);

        Assert.True(result.Succeeded);
        var replayed = new List<StoredFact>();
        await foreach (var fact in store.ReplayAsync(new ReplayQuery(TaskId: taskId), CancellationToken.None))
        {
            replayed.Add(fact);
        }

        Assert.Contains(replayed, f => f.EventName == "TaskCompletedFact");
        Assert.All(replayed, f => Assert.Equal("fs-a", f.Capability)); // Capability = 实例名
    }

    [Fact]
    public async Task Capability_actor_persists_task_failure_facts()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var system = new ActorSystem();
        var domain = CapabilityDomain.Attach(system, "fs");
        var store = new InMemoryEventStore();
        var taskId = Guid.NewGuid();

        var handle = domain.Spawn(
            "fs-a",
            envelope => Task.FromResult(new TaskResultEnvelope
            {
                TaskId = envelope.TaskId,
                Succeeded = false,
                Type = TaskResultType.Failed,
                ErrorCode = "KS:CORE:TEST",
            }),
            eventStore: store);

        var result = await domain.RequestAsync(handle, new TaskEnvelope
        {
            TaskId = taskId,
            Capability = "fs",
            Operation = "read",
            PayloadBytes = [],
        }, cts.Token);

        Assert.False(result.Succeeded);
        var replayed = new List<StoredFact>();
        await foreach (var fact in store.ReplayAsync(new ReplayQuery(TaskId: taskId), CancellationToken.None))
        {
            replayed.Add(fact);
        }

        Assert.Contains(replayed, f => f.EventName == "TaskFailedFact");
    }
}

public class PluginLifecycleFactTests
{
    [Fact]
    public async Task Runtime_persists_lifecycle_facts_on_active_and_failed()
    {
        // DC-11：PluginRuntime 生命周期事实（启动/失败必须存活，ADR-0009/03 §4）
        var store = new InMemoryEventStore();
        var registry = new Keystone.Runtime.Plugins.Services.ServiceRegistry();

        var manifestOk = new Keystone.Runtime.Plugins.Manifest.PluginManifest("ok", "1.0.0", "A.cs", [], [], []);
        var manifestFail = new Keystone.Runtime.Plugins.Manifest.PluginManifest("fail", "1.0.0", "B.cs", [], [], []);

        var runtimeOk = new Keystone.Runtime.Plugins.Lifecycle.PluginRuntime(
            manifestOk,
            _ => new OkPlugin(),
            registry,
            _ => new Keystone.Runtime.Context.ContextFacade("ok", eventStore: store));
        await runtimeOk.StartAsync();

        var runtimeFail = new Keystone.Runtime.Plugins.Lifecycle.PluginRuntime(
            manifestFail,
            _ => new FailingPlugin(),
            registry,
            _ => new Keystone.Runtime.Context.ContextFacade("fail", eventStore: store));
        await runtimeFail.StartAsync(); // → FAILED

        var names = new List<string>();
        await foreach (var fact in store.ReplayAsync(new ReplayQuery(), CancellationToken.None))
        {
            names.Add(fact.EventName ?? string.Empty);
        }

        Assert.Contains("PluginStartedFact", names);
        Assert.Contains("PluginFailedFact", names);
    }

    private sealed class OkPlugin : Keystone.Runtime.Plugins.Lifecycle.IPlugin
    {
        public Task InitializeAsync(
            Keystone.Runtime.Context.IPluginContext context,
            IReadOnlyDictionary<string, object?> config)
            => Task.CompletedTask;

        public Task DisposeAsync() => Task.CompletedTask;
    }

    private sealed class FailingPlugin : Keystone.Runtime.Plugins.Lifecycle.IPlugin
    {
        public Task InitializeAsync(
            Keystone.Runtime.Context.IPluginContext context,
            IReadOnlyDictionary<string, object?> config)
            => throw new InvalidOperationException("boom");

        public Task DisposeAsync() => Task.CompletedTask;
    }
}
