using System.Diagnostics;
using Keystone.Core.Contracts;
using Keystone.Runtime.Actors;
using Keystone.Runtime.Context;
using Keystone.Runtime.Pipeline;
using Keystone.Runtime.Trace;
using Proto;

namespace Keystone.Runtime.Tests;

/// <summary>
/// DC-13（17-doc-compliance-audit / 06 §3-§4 / 05 §5）：Trace 接入能力域调用链 + TaskId 幂等去重。
/// 修复前：TraceContext 零调用；无幂等机制——重试重复副作用。
/// 兑现：请求执行包裹 keystone.task Activity（TaskId/ParentTaskId/能力域/操作 tag 贯穿，
/// 中间件/服务内读 Activity.Current 即得）；重复 TaskId 二次请求返回缓存结果不重执行。
/// </summary>
public class TraceWiringTests
{
    private sealed class TraceReadingMiddleware(List<string> observed) : IMiddleware
    {
        public string Id => "trace-reader";

        public int Order => 0;

        public async Task InvokeAsync(IPluginContext ctx, RequestDelegate next)
        {
            var current = TraceContext.GetCurrentTaskId();
            observed.Add(current == default ? "none" : current.ToString());
            await next(ctx);
        }
    }

    private static TaskEnvelope Envelope(Guid taskId, Guid? parent = null) => new()
    {
        TaskId = taskId,
        ParentTaskId = parent,
        Capability = "fs",
        Operation = "read",
        PayloadBytes = [],
    };

    private static TaskResultEnvelope Ok(TaskEnvelope e) => new()
    {
        TaskId = e.TaskId,
        Succeeded = true,
        Type = TaskResultType.Completed,
    };

    [Fact]
    public async Task Request_execution_carries_trace_context()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var system = new ActorSystem();
        var domain = CapabilityDomain.Attach(system, "fs");
        var observed = new List<string>();
        var taskId = Guid.NewGuid();

        var handle = domain.Spawn("fs-a", e => Task.FromResult(Ok(e)),
            [new TraceReadingMiddleware(observed)]);

        var result = await domain.RequestAsync(handle, Envelope(taskId), cts.Token);

        Assert.True(result.Succeeded);
        Assert.Equal([taskId.ToString()], observed); // 中间件内读 Activity.Current 得 TaskId（H1）

        // Activity 收尾：请求结束后 Activity.Current 不残留本任务的 activity
        Assert.NotEqual(taskId.ToString(), TraceContext.GetCurrentTaskId().ToString());
    }

    [Fact]
    public async Task Activity_carries_capability_and_operation_tags()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var system = new ActorSystem();
        var domain = CapabilityDomain.Attach(system, "fs");
        Activity? captured = null;
        var parent = Guid.NewGuid();

        var handle = domain.Spawn("fs-a", e => Task.FromResult(Ok(e)),
            [new TagCapturingMiddleware(() => captured = Activity.Current)]);

        var result = await domain.RequestAsync(
            handle, Envelope(Guid.NewGuid(), parent: parent), cts.Token);

        Assert.True(result.Succeeded);
        Assert.NotNull(captured);
        Assert.Equal("fs", captured!.GetTagItem(TraceContext.CapabilityTag));
        Assert.Equal("read", captured.GetTagItem(TraceContext.OperationTag));
        Assert.Equal(parent.ToString(), captured.GetTagItem(TraceContext.ParentTaskIdTag));
        Assert.Equal(TraceContext.ActivityName, captured.OperationName);
    }

    private sealed class TagCapturingMiddleware(Action capture) : IMiddleware
    {
        public string Id => "tag-capture";

        public int Order => 0;

        public async Task InvokeAsync(IPluginContext ctx, RequestDelegate next)
        {
            capture();
            await next(ctx);
        }
    }

    [Fact]
    public async Task Duplicate_task_id_returns_cached_result_without_reexecution()
    {
        // 06 §4 幂等契约：TaskId 即幂等键——重试不得重复执行副作用
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var system = new ActorSystem();
        var domain = CapabilityDomain.Attach(system, "fs");
        var executions = 0;
        var taskId = Guid.NewGuid();

        var handle = domain.Spawn("fs-a", e =>
        {
            executions++;
            return Task.FromResult(Ok(e));
        });

        var first = await domain.RequestAsync(handle, Envelope(taskId), cts.Token);
        var second = await domain.RequestAsync(handle, Envelope(taskId), cts.Token); // 重试/重复投递

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(1, executions); // handler 仅执行一次（幂等去重）
    }
}
