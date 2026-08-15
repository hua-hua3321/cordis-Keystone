using Keystone.Core.Contracts;
using Proto;

namespace Keystone.Runtime.Actors;

/// <summary>
/// 能力域（01 §4 多实例模型）：一个能力域可 spawn 多个实例 actor，各自独立 context
/// （服务级隔离 03 §2.2）。跨域请求携带 TaskEnvelope，响应携带 TaskResultEnvelope。
/// 15-decoupling-plan D1（C1）：Proto.Actor（ActorSystem/PID）内聚于本类，公共面只暴露框架类型
/// <see cref="CapabilityHandle"/>——调用方无需引用 Proto.Actor。
/// </summary>
public sealed class CapabilityDomain : IAsyncDisposable
{
    private readonly ActorSystem _system;
    private readonly string _name;
    private readonly bool _ownsSystem;

    private CapabilityDomain(ActorSystem system, string name, bool ownsSystem)
    {
        _system = system;
        _name = name;
        _ownsSystem = ownsSystem;
    }

    /// <summary>创建能力域（内部持有独立 ActorSystem；宿主/管理层生命周期管理，Dispose 时释放）。</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2000",
        Justification = "ActorSystem 所有权转移给 CapabilityDomain，由本域 DisposeAsync 统一释放")]
    public static CapabilityDomain Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new CapabilityDomain(new ActorSystem(), name, ownsSystem: true);
    }

    /// <summary>
    /// 创建能力域并注入既有 ActorSystem（测试缝 / 多域共享系统场景；调用方负责 system 生命周期）。
    /// </summary>
    public static CapabilityDomain Attach(ActorSystem system, string name)
    {
        ArgumentNullException.ThrowIfNull(system);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new CapabilityDomain(system, name, ownsSystem: false);
    }

    /// <summary>Spawn 一个能力域实例（instanceName 唯一；handler 处理跨域请求）。</summary>
    public CapabilityHandle Spawn(string instanceName, Func<TaskEnvelope, Task<TaskResultEnvelope>> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        ArgumentNullException.ThrowIfNull(handler);

        var props = Props.FromProducer(() => new CapabilityActor(handler));
        var pid = _system.Root.SpawnNamed(props, $"{_name}-{instanceName}");
        return new CapabilityHandle(this, pid);
    }

    /// <summary>跨域请求（等待响应；TaskId 贯穿，06 §1）。</summary>
    public Task<TaskResultEnvelope> RequestAsync(CapabilityHandle handle, TaskEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handle);
        return RequestCoreAsync(handle.Pid, envelope, cancellationToken);
    }

    internal async Task<TaskResultEnvelope> RequestCoreAsync(PID pid, TaskEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pid);
        ArgumentNullException.ThrowIfNull(envelope);

        var response = await _system.Root
            .RequestAsync<DomainResponse>(pid, new DomainRequest(envelope), cancellationToken)
            .ConfigureAwait(false);
        return response.Envelope;
    }

    /// <summary>释放内部 ActorSystem（仅 Create 模式拥有；Attach 模式由调用方管理）。</summary>
    public async ValueTask DisposeAsync()
    {
        if (_ownsSystem)
        {
            await _system.ShutdownAsync().ConfigureAwait(false);
        }
    }
}
