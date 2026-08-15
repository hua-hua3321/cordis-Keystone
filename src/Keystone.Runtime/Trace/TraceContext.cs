using System.Diagnostics;
using Keystone.Core.Contracts;

namespace Keystone.Runtime.Trace;

/// <summary>
/// 任务链路上下文（H1 落地，05 §5）：System.Diagnostics.Activity 承载，
/// Activity.Current 经 async 自动贯穿——服务方法内读 Activity.Current 即得调用方上下文（无需参数传递）。
/// </summary>
public static class TraceContext
{
    public const string ActivityName = "keystone.task";
    public const string TaskIdTag = "keystone.task.id";
    public const string ParentTaskIdTag = "keystone.task.parent";
    public const string CapabilityTag = "keystone.capability";
    public const string OperationTag = "keystone.operation";

    /// <summary>启动任务链路（创建并 Start Activity，携带 TaskId/ParentTaskId/能力域/操作）。</summary>
    public static Activity StartTask(TaskId taskId, string capability, string operation, TaskId? parentTaskId = null)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(operation);

        var activity = new Activity(ActivityName);
        activity.SetTag(TaskIdTag, taskId.ToString());
        activity.SetTag(CapabilityTag, capability);
        activity.SetTag(OperationTag, operation);
        if (parentTaskId is not null)
        {
            activity.SetTag(ParentTaskIdTag, parentTaskId.Value.ToString());
        }

        activity.Start();
        return activity;
    }

    /// <summary>读取环境中的任务标识（H1：服务内读 Activity.Current 得调用方 TaskId）。</summary>
    public static TaskId GetCurrentTaskId()
    {
        var tag = Activity.Current?.GetTagItem(TaskIdTag) as string;
        return tag is not null && TaskId.TryParse(tag, out var id) ? id : default;
    }
}
