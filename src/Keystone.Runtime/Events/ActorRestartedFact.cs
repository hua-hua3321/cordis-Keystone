namespace Keystone.Runtime.Events;

/// <summary>
/// 监督重启事实（P70-T3，ADR-0018 L2）：OneForOne Restart 决策发生——审计流可回放
/// "哪个实例何时因何重启"（05 §2 "重启计数→告警" 的事实数据基础）。
/// 由宿主在 CapabilityDomain.Create 接线（监督回调 → 根总线 fire-and-forget）。
/// </summary>
public sealed record ActorRestartedFact(string InstanceName, string Reason) : IFactEvent
{
    /// <summary>无任务关联（监督动作非请求路径）。</summary>
    public Guid TaskId => Guid.Empty;

    /// <summary>实例名（能力域事实维度）。</summary>
    public string? Capability => InstanceName;

    public byte[]? Payload => null;

    /// <summary>尽力写（监督事实不阻塞监督路径——ADR-0009 决策 3）。</summary>
    public bool Durable => false;
}
