namespace Keystone.Core.Contracts;

/// <summary>
/// 任务标识（Value Object，doc 06 §3/§4）：全局唯一幂等键 + 跨域编排树的节点标识（ADR-0004）。
/// 层级关系由 <see cref="TaskRequest.ParentTaskId"/> 表达（父引用）；TaskId 自身保证唯一、可比较、可解析。
/// </summary>
public readonly record struct TaskId(Guid Value) : IComparable<TaskId>
{
    /// <summary>新建根任务标识（幂等键）。</summary>
    public static TaskId New() => new(Guid.NewGuid());

    /// <summary>新建子任务标识（唯一性由 Guid 保证；父引用由调用方放入 ParentTaskId）。</summary>
    public static TaskId CreateChild() => new(Guid.NewGuid());

    /// <summary>从字符串解析（Guid "D" 格式）。</summary>
    public static TaskId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new TaskId(Guid.Parse(value));
    }

    /// <summary>宽松解析，失败时返回 false 并输出默认值。</summary>
    public static bool TryParse(string? value, out TaskId id)
    {
        if (Guid.TryParse(value, out var guid))
        {
            id = new TaskId(guid);
            return true;
        }

        id = default;
        return false;
    }

    public int CompareTo(TaskId other) => Value.CompareTo(other.Value);

    public static bool operator <(TaskId left, TaskId right) => left.CompareTo(right) < 0;

    public static bool operator <=(TaskId left, TaskId right) => left.CompareTo(right) <= 0;

    public static bool operator >(TaskId left, TaskId right) => left.CompareTo(right) > 0;

    public static bool operator >=(TaskId left, TaskId right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString("D");
}
