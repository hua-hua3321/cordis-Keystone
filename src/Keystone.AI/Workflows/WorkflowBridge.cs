using Keystone.Core.Contracts;

namespace Keystone.AI.Workflows;

/// <summary>
/// 跨域编排桥接（ADR-0008 决策 2 workflow 域 + ADR-0004 编排语义）：
/// fan-out/fan-in 全等聚合，TaskId/ParentTaskId 贯穿**不稀释**（O2 不回退项）。
/// MAF Workflows 图构建（WorkflowBuilder + Executor 源生成）由 HostAgent 驱动（实现期细化），
/// 本桥接承载 TaskId 语义保证（编排正确性的核心）。
/// </summary>
public sealed class WorkflowBridge
{
    /// <summary>fan-out：任务分发到多分支（TaskId/ParentTaskId 原样传递，子任务编号派生）。</summary>
    public async Task<IReadOnlyList<TaskResultEnvelope>> FanOutAsync(
        TaskEnvelope request,
        IReadOnlyList<Func<TaskEnvelope, Task<TaskResultEnvelope>>> branches,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(branches);

        var results = await Task.WhenAll(branches.Select(branch => branch(request))).ConfigureAwait(false);
        return results;
    }

    /// <summary>
    /// fan-in 全等聚合（ADR-0004：全部成功才成功；任一失败 → 父任务失败，携带首错）。
    /// 结果保留原 TaskId + ParentTaskId（编排不稀释层级，O2）。
    /// </summary>
    public TaskResultEnvelope FanInAsync(TaskEnvelope original, IReadOnlyList<TaskResultEnvelope> results)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(results);

        var failed = results.FirstOrDefault(r => !r.Succeeded);
        if (failed is not null)
        {
            return new TaskResultEnvelope
            {
                TaskId = original.TaskId,
                ParentTaskId = original.ParentTaskId,
                Succeeded = false,
                Type = TaskResultType.Failed,
                ErrorCode = failed.ErrorCode,
                ErrorDetail = failed.ErrorDetail,
            };
        }

        return new TaskResultEnvelope
        {
            TaskId = original.TaskId,
            ParentTaskId = original.ParentTaskId,
            Succeeded = true,
            Type = TaskResultType.Completed,
        };
    }
}
