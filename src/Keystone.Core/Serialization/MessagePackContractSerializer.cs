using MessagePack;

namespace Keystone.Core.Serialization;

/// <summary>
/// MessagePack 契约序列化器（ADR-0004 默认实现）：契约类型带 [MessagePackObject] 源生成，
/// AOT 安全。15-decoupling-plan D3：事件持久化等序列化边界的默认实现。
/// </summary>
public sealed class MessagePackContractSerializer : IContractSerializer
{
    /// <inheritdoc />
    public byte[] Serialize<T>(T value)
        => MessagePackSerializer.Serialize(value);

    /// <inheritdoc />
    public T Deserialize<T>(byte[] data)
        => MessagePackSerializer.Deserialize<T>(data);
}
