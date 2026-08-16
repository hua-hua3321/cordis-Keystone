namespace Keystone.Runtime.Events;

/// <summary>
/// 实例停止事实（P70-T5，ADR-0018 L2）：actor 实例停止（域 Dispose / 系统关闭）发生——
/// 与 <see cref="ActorRestartedFact"/> 对称补齐 ADR L2 承诺（监督/生命周期动作入审计流，
/// 可回放"哪个实例何时停止"）。
/// 由 <see cref="Keystone.Runtime.Actors.CapabilityActor"/> 在收到 Proto.Actor <c>Stopped</c>
/// 系统消息时经实例 context 总线 emit（await 保证落盘后再终止）。
/// </summary>
public sealed record ActorStoppedFact(string InstanceName) : IFactEvent
{
    /// <summary>无任务关联（生命周期动作非请求路径）。</summary>
    public Guid TaskId => Guid.Empty;

    /// <summary>实例名（能力域事实维度）。</summary>
    public string? Capability => InstanceName;

    public byte[]? Payload => null;

    /// <summary>尽力写（停止事实不阻塞停止路径——ADR-0009 决策 3）。</summary>
    public bool Durable => false;
}
