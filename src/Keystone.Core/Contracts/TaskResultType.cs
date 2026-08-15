namespace Keystone.Core.Contracts;

/// <summary>任务结果类型（doc 06 §1）。</summary>
public enum TaskResultType
{
    Completed = 0,
    Failed = 1,
    Cancelled = 2,
}
