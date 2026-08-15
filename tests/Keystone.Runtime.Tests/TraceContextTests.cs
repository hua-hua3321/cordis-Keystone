using System.Diagnostics;
using Keystone.Core.Contracts;
using Keystone.Runtime.Trace;

namespace Keystone.Runtime.Tests;

public class TraceContextTests
{
    [Fact]
    public void StartTask_creates_activity_with_task_tags()
    {
        var taskId = TaskId.New();
        using var activity = TraceContext.StartTask(taskId, "fs", "read");

        Assert.NotNull(activity);
        Assert.Equal("keystone.task", activity.OperationName);
        Assert.Equal(taskId.ToString(), activity.GetTagItem(TraceContext.TaskIdTag));
        Assert.Equal("fs", activity.GetTagItem(TraceContext.CapabilityTag));
        Assert.Equal("read", activity.GetTagItem(TraceContext.OperationTag));
    }

    [Fact]
    public async Task Activity_flows_across_async_and_reads_current_context()
    {
        // H1 验收：服务方法内读 Activity.Current 得调用方上下文（无需参数传递）
        var taskId = TaskId.New();

        System.Diagnostics.ActivityTraceId? traceIdSeen = null;
        string? taskIdSeen = null;
        using var activity = TraceContext.StartTask(taskId, "fs", "read");

        await Task.Run(() =>
        {
            var current = Activity.Current;
            traceIdSeen = current?.TraceId;
            taskIdSeen = current?.GetTagItem(TraceContext.TaskIdTag) as string;
        });

        Assert.NotNull(traceIdSeen);
        Assert.Equal(activity.TraceId, traceIdSeen.Value); // Activity 跨 async 贯穿
        Assert.Equal(taskId.ToString(), taskIdSeen);       // 服务内读到调用方 TaskId
    }

    [Fact]
    public void Parent_task_tag_carried()
    {
        var taskId = TaskId.New();
        var parentId = TaskId.New();
        using var activity = TraceContext.StartTask(taskId, "fs", "read", parentId);

        Assert.Equal(parentId.ToString(), activity.GetTagItem(TraceContext.ParentTaskIdTag));
    }

    [Fact]
    public void GetCurrentTaskId_reads_ambient_activity()
    {
        var taskId = TaskId.New();
        using var activity = TraceContext.StartTask(taskId, "fs", "read");

        Assert.Equal(taskId, TraceContext.GetCurrentTaskId());
    }

    [Fact]
    public void GetCurrentTaskId_returns_default_without_activity()
    {
        Assert.Equal(default, TraceContext.GetCurrentTaskId());
    }
}
