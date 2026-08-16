using System.Diagnostics;
using Keystone.Core.Contracts;

namespace Keystone.Runtime.Trace;

/// <summary>
/// 任务链路上下文（H1 落地，05 §5；P70-T1/ADR-0018 迁移）：ActivitySource 承载——
/// OTel 导出器可见 + 采样协商；Activity.Current 经 async 自动贯穿——服务方法内读
/// Activity.Current 即得调用方上下文（无需参数传递）。
/// 功能保底：静态安装仅应答本 source 的 listener（恒 AllData）——无任何导出 listener
/// （嵌入方未配置 Observability）时 StartTask 仍返回非空且标签可读：<see cref="GetCurrentTaskId"/>
/// 是功能性依赖（RingBufferLoggerProvider 日志 taskId 标签），非纯观测。
/// 代价注记：保底 listener 令 IsAllDataRequested 恒真——采样率只控制导出，不控制
/// 进程内标签存储（功能读回需要标签，刻意取舍）。
/// </summary>
public static class TraceContext
{
    /// <summary>本程序集观测源名（ADR-0018 L1：span/meter 统一归属）。</summary>
    public const string SourceName = "Keystone.Runtime";

    private static readonly ActivitySource Source = new(SourceName);

    static TraceContext()
    {
        // 功能保底 listener：只应答本 source——不干扰其他库的 ActivitySource；
        // AllData 保证标签存储（PropagationData 语义下标签可能被运行时跳过，不取）
        // CA2000：刻意进程级存活（AddActivityListener 注册即全局持引）——Dispose 即断功能保底
#pragma warning disable CA2000
        ActivitySource.AddActivityListener(new ActivityListener
#pragma warning restore CA2000
        {
            ShouldListenTo = s => ReferenceEquals(s, Source),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        });
    }

    public const string ActivityName = "keystone.task";
    public const string TaskIdTag = "keystone.task.id";
    public const string ParentTaskIdTag = "keystone.task.parent";
    public const string CapabilityTag = "keystone.capability";
    public const string OperationTag = "keystone.operation";

    // P70-T4（ADR-0018 L1）：config/host 切片 span 名 + tag 键（Hosting 组合层接线使用）
    public const string ConfigApplyActivityName = "keystone.config.apply";
    public const string ConfigEntryActivityName = "keystone.config.entry";
    public const string HotUpdateActivityName = "keystone.hotupdate";
    public const string GroupTransactionActivityName = "keystone.group.transaction";

    public const string EntryIdTag = "keystone.entry.id";
    public const string ChannelTag = "keystone.channel";
    public const string GroupTag = "keystone.group";
    public const string OutcomeTag = "keystone.outcome";
    public const string EntriesTag = "keystone.entries";
    public const string FailuresTag = "keystone.failures";
    public const string RolledBackTag = "keystone.rolled_back";
    public const string OldKeysTag = "keystone.hotupdate.old_keys";
    public const string NewKeysTag = "keystone.hotupdate.new_keys";

    /// <summary>供宿主/组合根引用的源（OTel AddSource 接线，ADR-0018 L3）。</summary>
    public static ActivitySource SourceForHosting => Source;

    /// <summary>启动任务链路（创建并 Start Activity，携带 TaskId/ParentTaskId/能力域/操作）。
    /// 非空保证：功能保底 listener 恒应答（见类注记）。</summary>
    public static Activity StartTask(TaskId taskId, string capability, string operation, TaskId? parentTaskId = null)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(operation);

        var activity = Source.StartActivity(ActivityName);
        if (activity is null)
        {
            // 保底 listener 契约断裂 = 配置性 bug（非调用方参数错）——MA0015 不适用
            throw new InvalidOperationException(
                "TraceContext functional guarantee listener missing; StartTask requires non-null activity");
        }
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
