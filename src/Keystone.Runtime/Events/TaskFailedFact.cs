namespace Keystone.Runtime.Events;

/// <summary>任务失败事实（DC-11，04 §7/03 §4：任务失败必须存活；能力域 actor 发布，尽力写）。</summary>
public sealed record TaskFailedFact(Guid TaskId, string? Capability, string? ErrorCode) : IFactEvent
{
    public byte[]? Payload => null;

    public bool Durable => false;
}
