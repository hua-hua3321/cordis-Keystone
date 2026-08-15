using Keystone.Core.Errors;

namespace Keystone.Core.Contracts;

/// <summary>
/// 任务结果（接口层形状，doc 06 §1）。<see cref="ErrorCode"/> 码表与
/// <see cref="KeystoneException.Code"/> 共用（M6，doc 12 §8）。
/// 跨域序列化使用 <see cref="TaskResultEnvelope"/>。
/// </summary>
public sealed record TaskResult(
    TaskId TaskId,
    bool Succeeded,
    TaskResultType Type,
    object? Data,
    string? ErrorCode,
    string? ErrorDetail)
{
    public static TaskResult Completed(TaskId taskId, object? data = null)
        => new(taskId, Succeeded: true, TaskResultType.Completed, data, null, null);

    public static TaskResult Failed(TaskId taskId, string errorCode, string? detail = null)
        => new(taskId, Succeeded: false, TaskResultType.Failed, null, errorCode, detail);

    public static TaskResult Cancelled(TaskId taskId)
        => new(taskId, Succeeded: false, TaskResultType.Cancelled, null, null, null);
}
