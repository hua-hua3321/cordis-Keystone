using Keystone.Core.Contracts;
using Keystone.Runtime.Pipeline;
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

    /// <summary>
    /// Spawn 一个能力域实例（instanceName 唯一；handler 处理跨域请求）。
    /// <paramref name="middlewares"/> = 插件中间件链（01 §2：actor 持管道，中间件包裹 handler，
    /// before/after/短路语义，ADR-0006 waterfall）。
    /// <paramref name="parentContext"/> = 实例 context 的父（01 §4：接入宿主 root 的服务链 + 共享事件总线；
    /// 缺省 null = 独立实例）。
    /// <paramref name="supervision"/> = 监督策略（05 §2/09 §3，DC-4）：OneForOne（默认 Restart decider +
    /// 3 次重试/5s 窗口，超阈值停止不再重启 = 域不可用）；可自定义。
    /// </summary>
    public CapabilityHandle Spawn(
        string instanceName,
        Func<TaskEnvelope, Task<TaskResultEnvelope>> handler,
        IReadOnlyList<IMiddleware>? middlewares = null,
        Keystone.Runtime.Context.IContext? parentContext = null,
        CapabilitySupervisionOptions? supervision = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        ArgumentNullException.ThrowIfNull(handler);

        supervision ??= new CapabilitySupervisionOptions();
        var props = Props.FromProducer(() => new CapabilityActor(instanceName, handler, middlewares, parentContext))
            .WithGuardianSupervisorStrategy(new OneForOneStrategy(
                decider: (_, _) => Proto.SupervisorDirective.Restart, // DC-4：崩溃重启（默认）
                maxNrOfRetries: supervision.MaxRestarts,
                withinTimeSpan: supervision.RestartWindow));
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
