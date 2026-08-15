using Keystone.Core.Contracts;
using Proto;

namespace Keystone.Runtime.Actors;

/// <summary>
/// 能力域 actor（T1 Proto.Actor，01 §2-§3）：串行消息循环（actor 原生，context 无竞争）
/// + 监督重启（Proto.Actor 默认策略）。每次消息 = 一次跨域请求处理。
/// 15-decoupling-plan D1（C1b）：internal 实现细节——IActor/IContext 不外泄。
/// </summary>
internal sealed class CapabilityActor : IActor
{
    private readonly Func<TaskEnvelope, Task<TaskResultEnvelope>> _handler;

    public CapabilityActor(Func<TaskEnvelope, Task<TaskResultEnvelope>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
    }

    public async Task ReceiveAsync(IContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        switch (context.Message)
        {
            case DomainRequest { Envelope: var envelope }:
                var result = await _handler(envelope).ConfigureAwait(false);
                context.Respond(new DomainResponse(result));
                break;
        }
    }
}
