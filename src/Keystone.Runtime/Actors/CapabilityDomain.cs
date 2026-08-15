using Keystone.Core.Contracts;
using Proto;

namespace Keystone.Runtime.Actors;

/// <summary>
/// 能力域（01 §4 多实例模型）：一个能力域可 spawn 多个实例 actor，各自独立 context
/// （服务级隔离 03 §2.2）。跨域请求携带 TaskEnvelope，响应携带 TaskResultEnvelope。
/// </summary>
public sealed class CapabilityDomain
{
    private readonly ActorSystem _system;
    private readonly string _name;

    public CapabilityDomain(ActorSystem system, string name)
    {
        ArgumentNullException.ThrowIfNull(system);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _system = system;
        _name = name;
    }

    /// <summary>Spawn 一个能力域实例 actor（instanceName 唯一；handler 处理跨域请求）。</summary>
    public PID Spawn(string instanceName, Func<TaskEnvelope, Task<TaskResultEnvelope>> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        ArgumentNullException.ThrowIfNull(handler);

        var props = Props.FromProducer(() => new CapabilityActor(handler));
        return _system.Root.SpawnNamed(props, $"{_name}-{instanceName}");
    }

    /// <summary>跨域请求（等待响应；TaskId 贯穿，06 §1）。</summary>
    public async Task<TaskResultEnvelope> RequestAsync(PID pid, TaskEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pid);
        ArgumentNullException.ThrowIfNull(envelope);

        var response = await _system.Root
            .RequestAsync<DomainResponse>(pid, new DomainRequest(envelope), cancellationToken)
            .ConfigureAwait(false);
        return response.Envelope;
    }
}
