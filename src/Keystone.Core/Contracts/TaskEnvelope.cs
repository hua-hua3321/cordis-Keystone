using MessagePack;

namespace Keystone.Core.Contracts;

/// <summary>
/// 跨域序列化信封（DTO，[MessagePackObject] 源生成，规则 0 第 3 条）：
/// 任务元数据 + 载荷字节（<see cref="PayloadBytes"/>）。载荷的具体类型由能力域契约序列化
/// （doc 06 §6：跨域边界显式序列化契约，MessagePack 默认）。
/// </summary>
[MessagePackObject]
public sealed record TaskEnvelope
{
    [Key(0)]
    public Guid TaskId { get; init; }

    [Key(1)]
    public Guid? ParentTaskId { get; init; }

    [Key(2)]
    public string? Capability { get; init; }

    [Key(3)]
    public string? Operation { get; init; }

    [Key(4)]
    public byte[]? PayloadBytes { get; init; }

    /// <summary>从接口层任务构建信封（取消令牌不序列化——运行态传播）。</summary>
    public static TaskEnvelope FromRequest(TaskRequest request, byte[] payloadBytes)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(payloadBytes);

        return new TaskEnvelope
        {
            TaskId = request.TaskId.Value,
            ParentTaskId = request.ParentTaskId?.Value,
            Capability = request.Capability,
            Operation = request.Operation,
            PayloadBytes = payloadBytes,
        };
    }
}
