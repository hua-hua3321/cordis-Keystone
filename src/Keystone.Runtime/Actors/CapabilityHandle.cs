using Keystone.Core.Contracts;
using Proto;

namespace Keystone.Runtime.Actors;

/// <summary>
/// 能力域实例句柄（框架自有类型，15-decoupling-plan D1/C1）：
/// 封装 Proto.Actor PID，调用方不接触 Proto 类型。由 <see cref="CapabilityDomain.Spawn"/> 返回。
/// </summary>
public sealed class CapabilityHandle
{
    private readonly CapabilityDomain _domain;
    private readonly PID _pid;

    internal CapabilityHandle(CapabilityDomain domain, PID pid)
    {
        _domain = domain;
        _pid = pid;
    }

    internal PID Pid => _pid;

    /// <summary>直接投递消息（DC-14 测试缝：绕过 RequestAsync 传输层取消，验证 actor 侧语义）。</summary>
    internal void SendRaw(object message, Proto.ActorSystem system) => system.Root.Send(_pid, message);

    /// <summary>跨域请求（等待响应；TaskId 贯穿，06 §1）。</summary>
    public Task<TaskResultEnvelope> RequestAsync(TaskEnvelope envelope, CancellationToken cancellationToken)
        => _domain.RequestCoreAsync(_pid, envelope, cancellationToken);
}
