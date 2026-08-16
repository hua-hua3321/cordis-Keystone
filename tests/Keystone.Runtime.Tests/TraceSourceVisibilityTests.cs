using System.Diagnostics;
using Keystone.Core.Contracts;
using Keystone.Runtime.Trace;

namespace Keystone.Runtime.Tests;

/// <summary>
/// P70-T1（ADR-0018）：TraceContext 迁移 ActivitySource——OTel 可见性 + 功能保底。
/// 修复前 <c>new Activity(...)</c>：无 ActivitySource 归属——OTel 导出器（只订阅
/// ActivitySource）不可见、无采样协商；且无任何 listener 时 StartActivity 返 null 会断
/// GetCurrentTaskId（RingBufferLoggerProvider 的功能性依赖——日志 taskId 标签）。
/// </summary>
public class TraceSourceVisibilityTests
{
    private const string SourceName = "Keystone.Runtime";

    [Fact]
    public void StartTask_is_visible_to_ActivitySource_listeners()
    {
        // 红：现行 new Activity 实现下，listener 永远看不到（无 source 归属）
        var started = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = a => started.Add(a),
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = TraceContext.StartTask(new TaskId(Guid.NewGuid()), "fs", "read");

        Assert.NotNull(activity);
        var seen = Assert.Single(started);
        Assert.Equal(TraceContext.ActivityName, seen.OperationName);
        Assert.Equal("fs", seen.GetTagItem(TraceContext.CapabilityTag) as string);
        Assert.Equal("read", seen.GetTagItem(TraceContext.OperationTag) as string);
    }

    [Fact]
    public void StartTask_never_returns_null_even_without_export_listeners()
    {
        // 功能保底：无任何导出 listener（生产嵌入不配置 Observability）时 activity 仍非空、
        // 标签仍可读——GetCurrentTaskId 功能不断（RingBuffer 日志 taskId 标签依赖）
        using var activity = TraceContext.StartTask(
            new TaskId(Guid.NewGuid()), "fs", "write", parentTaskId: new TaskId(Guid.NewGuid()));

        Assert.NotNull(activity);
        Assert.NotEqual(default, TraceContext.GetCurrentTaskId());
        Assert.NotNull(activity.GetTagItem(TraceContext.ParentTaskIdTag));
    }

    [Fact]
    public void Functional_listener_does_not_duplicate_parent_context()
    {
        // 保底 listener 不引入副作用：parent 链语义与迁移前一致（Activity.Current 演进）
        var outer = TraceContext.StartTask(new TaskId(Guid.NewGuid()), "fs", "outer");
        var inner = TraceContext.StartTask(
            new TaskId(Guid.NewGuid()), "fs", "inner", parentTaskId: new TaskId(Guid.NewGuid()));
        Assert.Same(outer, inner.Parent); // 环境父（Activity.Current）优先——与 new Activity 行为一致
        outer.Dispose();
        inner.Dispose();
    }
}
