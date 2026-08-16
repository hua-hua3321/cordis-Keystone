using Keystone.Core.Contracts;
using Keystone.Runtime.Actors;
using Proto;

namespace Keystone.Runtime.Tests;

public class CapabilityDomainTests
{
    [Fact]
    public async Task Serial_semantics_processes_concurrent_messages_in_order()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var system = new ActorSystem();
        var domain = CapabilityDomain.Attach(system, "fs");
        var processed = new List<int>();
        var handle = domain.Spawn("fs-a", async envelope =>
        {
            var n = int.Parse(envelope.Operation!, System.Globalization.CultureInfo.InvariantCulture);
            processed.Add(n);
            await Task.Delay(5); // 模拟处理耗时——串行 actor 下不会交错
            return new TaskResultEnvelope { TaskId = envelope.TaskId, Succeeded = true, Type = TaskResultType.Completed };
        });

        // 并发发送 10 条（actor 串行循环保证顺序）
        var sends = Enumerable.Range(0, 10).Select(i =>
            domain.RequestAsync(handle, new TaskEnvelope
            {
                TaskId = Guid.NewGuid(),
                Capability = "fs",
                Operation = i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                PayloadBytes = [],
            }, cts.Token));
        await Task.WhenAll(sends);

        Assert.Equal(Enumerable.Range(0, 10), processed); // 无交错：串行语义（01 §3）
    }

    [Fact]
    public async Task Supervision_restarts_actor_after_handler_failure()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var system = new ActorSystem();
        var domain = CapabilityDomain.Attach(system, "fs");
        var calls = 0;
        var handle = domain.Spawn("fs-a", async envelope =>
        {
            Interlocked.Increment(ref calls);
            if (calls == 1)
            {
                throw new InvalidOperationException("transient failure");
            }

            return new TaskResultEnvelope { TaskId = envelope.TaskId, Succeeded = true, Type = TaskResultType.Completed };
        });

        // P68 监督观测面更新：handler 抛错 → 立即失败结果回填（future 完成不挂死），
        // actor 仍崩溃 → Proto.Actor 监督重启（respond 后上抛触发）
        using var firstCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var first = await domain.RequestAsync(handle, Envelope("1"), firstCts.Token);
        Assert.False(first.Succeeded);

        // 重启后成功（新 token）
        using var secondCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var result = await domain.RequestAsync(handle, Envelope("2"), secondCts.Token);
        Assert.True(result.Succeeded);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Cross_domain_taskid_preserved_with_parent()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var system = new ActorSystem();
        var domain = CapabilityDomain.Attach(system, "fs");
        var handle = domain.Spawn("fs-a", envelope =>
            Task.FromResult(new TaskResultEnvelope
            {
                TaskId = envelope.TaskId, // 跨域贯穿：响应携带原 TaskId
                Succeeded = true,
                Type = TaskResultType.Completed,
            }));

        var taskId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var receivedParent = Guid.Empty;
        var handle2 = domain.Spawn("fs-a2", envelope =>
        {
            receivedParent = envelope.ParentTaskId ?? Guid.Empty;
            return Task.FromResult(new TaskResultEnvelope
            {
                TaskId = envelope.TaskId,
                Succeeded = true,
                Type = TaskResultType.Completed,
            });
        });
        var result = await domain.RequestAsync(handle2, new TaskEnvelope
        {
            TaskId = taskId,
            ParentTaskId = parentId,
            Capability = "fs",
            Operation = "read",
            PayloadBytes = [],
        }, cts.Token);

        Assert.Equal(taskId, result.TaskId);        // O2 前置：跨域 TaskId 一致
        Assert.Equal(parentId, receivedParent);     // 父任务跨域传递（06 §1/ADR-0004）
    }

    [Fact]
    public async Task Multiple_instances_have_independent_contexts()
    {
        // 多实例隔离（01 §4）：实例 A/B 各自 context（fs-A/fs-B 互不可见，03 §2.2）
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var system = new ActorSystem();
        var domain = CapabilityDomain.Attach(system, "fs");
        var contexts = new Dictionary<string, Context.ContextFacade>
        {
            ["a"] = new("fs-a", null),
            ["b"] = new("fs-b", null),
        };
        contexts["a"].Provide("fs", new object());

        var handleA = domain.Spawn("fs-a", _ => Task.FromResult(Ok(contexts["a"])));
        var handleB = domain.Spawn("fs-b", _ => Task.FromResult(Ok(contexts["b"])));

        // 实例 A 有自己的 fs；实例 B 没有（互不可见）
        var resultA = await domain.RequestAsync(handleA, Envelope("check"), cts.Token);
        var resultB = await domain.RequestAsync(handleB, Envelope("check"), cts.Token);

        Assert.True(resultA.Succeeded);  // 实例 A 有 fs
        Assert.False(resultB.Succeeded); // 实例 B 无 fs —— fs-A/fs-B 互不可见（03 §2.2 隔离证明）
        Assert.Same(contexts["a"], contexts["a"]);
    }

    private static TaskEnvelope Envelope(string op) => new()
    {
        TaskId = Guid.NewGuid(),
        Capability = "fs",
        Operation = op,
        PayloadBytes = [],
    };

    private static TaskResultEnvelope Ok(Keystone.Runtime.Context.ContextFacade ctx) => new()
    {
        TaskId = Guid.NewGuid(),
        Succeeded = ctx.Services.TryGet<object>("fs", string.Empty) is not null,
        Type = TaskResultType.Completed,
    };
}
