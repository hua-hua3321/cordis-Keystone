using MessagePack;

namespace Keystone.Core.Contracts;

/// <summary>
/// 跨域结果信封（DTO，[MessagePackObject] 源生成）：结果元数据 + 数据字节
/// （<see cref="DataBytes"/>，具体类型由能力域契约序列化）。
/// </summary>
[MessagePackObject]
public sealed record TaskResultEnvelope
{
    [Key(0)]
    public Guid TaskId { get; init; }

    [Key(1)]
    public bool Succeeded { get; init; }

    [Key(2)]
    public TaskResultType Type { get; init; }

    [Key(3)]
    public string? ErrorCode { get; init; }

    [Key(4)]
    public string? ErrorDetail { get; init; }

    [Key(5)]
    public byte[]? DataBytes { get; init; }

    [Key(6)]
    public Guid? ParentTaskId { get; init; }

    /// <summary>从接口层结果构建信封。</summary>
    public static TaskResultEnvelope FromResult(TaskResult result, byte[] dataBytes)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(dataBytes);

        return new TaskResultEnvelope
        {
            TaskId = result.TaskId.Value,
            Succeeded = result.Succeeded,
            Type = result.Type,
            ErrorCode = result.ErrorCode,
            ErrorDetail = result.ErrorDetail,
            DataBytes = dataBytes,
        };
    }
}
