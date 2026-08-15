namespace Keystone.Runtime.Events;

/// <summary>任务完成事实（DC-11，04 §7/03 §4：任务完成必须存活；能力域 actor 发布，尽力写）。</summary>
public sealed record TaskCompletedFact(Guid TaskId, string? Capability) : IFactEvent
{
    /// <summary>任务结果载荷（暂空；结果信封序列化预留）。</summary>
    public byte[]? Payload => null;

    public bool Durable => false;
}
