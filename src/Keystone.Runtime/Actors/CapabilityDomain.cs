using Keystone.Core.Contracts;
using Keystone.Runtime.Events;
using Keystone.Runtime.Persistence;
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

    private readonly Action<SupervisionDecision>? _onSupervision;
    private readonly TimeSpan? _defaultSlowThreshold;
    private readonly List<(string Name, IEventStore? Store)> _spawned = []; // P70-T5：spawn 实例记账（停止时发 ActorStoppedFact）

    private CapabilityDomain(
        ActorSystem system, string name, bool ownsSystem,
        Action<SupervisionDecision>? onSupervision = null,
        TimeSpan? defaultSlowThreshold = null)
    {
        _system = system;
        _name = name;
        _ownsSystem = ownsSystem;
        _onSupervision = onSupervision;
        _defaultSlowThreshold = defaultSlowThreshold;
    }

    /// <summary>监督决策通知（P70-T3，ADR-0018）：Restart/Stop 决策 + 原因——宿主据此发事实/接线告警。</summary>
    public sealed record SupervisionDecision(string InstanceName, Exception Reason, string Directive);

    /// <summary>创建能力域（内部持有独立 ActorSystem；宿主/管理层生命周期管理，Dispose 时释放）。</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2000",
        Justification = "ActorSystem 所有权转移给 CapabilityDomain，由本域 DisposeAsync 统一释放")]
    public static CapabilityDomain Create(
        string name, Action<SupervisionDecision>? onSupervision = null, TimeSpan? slowRequestThreshold = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new CapabilityDomain(new ActorSystem(), name, ownsSystem: true, onSupervision, slowRequestThreshold);
    }

    /// <summary>
    /// 创建能力域并注入既有 ActorSystem（测试缝 / 多域共享系统场景；调用方负责 system 生命周期）。
    /// </summary>
    public static CapabilityDomain Attach(
        ActorSystem system, string name, Action<SupervisionDecision>? onSupervision = null, TimeSpan? slowRequestThreshold = null)
    {
        ArgumentNullException.ThrowIfNull(system);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new CapabilityDomain(system, name, ownsSystem: false, onSupervision, slowRequestThreshold);
    }

    /// <summary>
    /// Spawn 一个能力域实例（instanceName 唯一；handler 处理跨域请求）。
    /// <paramref name="middlewares"/> = 插件中间件链（01 §2：actor 持管道，中间件包裹 handler，
    /// before/after/短路语义，ADR-0006 waterfall）。
    /// <paramref name="parentContext"/> = 实例 context 的父（01 §4：接入宿主 root 的服务链 + 共享事件总线；
    /// 缺省 null = 独立实例）。
    /// <paramref name="supervision"/> = 监督策略（05 §2/09 §3，DC-4）：OneForOne（默认 Restart decider +
    /// 3 次重试/5s 窗口，超阈值停止不再重启 = 域不可用）；可自定义。
    /// <paramref name="eventStore"/> = 事实持久化（DC-11，ADR-0009）：任务完成/失败事实写入；
    /// parentContext 总线已携带存储时可省（共享总线）。
    /// </summary>
    public CapabilityHandle Spawn(
        string instanceName,
        Func<TaskEnvelope, Task<TaskResultEnvelope>> handler,
        IReadOnlyList<IMiddleware>? middlewares = null,
        Keystone.Runtime.Context.IContext? parentContext = null,
        CapabilitySupervisionOptions? supervision = null,
        Keystone.Runtime.Persistence.IEventStore? eventStore = null,
        TimeSpan? slowRequestThreshold = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        ArgumentNullException.ThrowIfNull(handler);

        supervision ??= new CapabilitySupervisionOptions();
        var slowThreshold = slowRequestThreshold ?? _defaultSlowThreshold;
        var onSupervision = _onSupervision;
        var domainPrefix = _name;
        var props = Props.FromProducer(
            () => new CapabilityActor(instanceName, handler, middlewares, parentContext, eventStore, slowThreshold))
            .WithGuardianSupervisorStrategy(new OneForOneStrategy(
                decider: (pid, reason) => WrapDecider(pid, reason, instanceName, domainPrefix, onSupervision),
                maxNrOfRetries: supervision.MaxRestarts,
                withinTimeSpan: supervision.RestartWindow));
        var pid = _system.Root.SpawnNamed(props, $"{_name}-{instanceName}");
        _spawned.Add((instanceName, eventStore));
        return new CapabilityHandle(this, pid);
    }

    /// <summary>
    /// decider 包装（P70-T3，ADR-0018 L2/L1）：Restart 决策 → restarts 计数 + 监督回调
    ///（宿主接线发 ActorRestartedFact）。监督路径不可被观测侧异常打断——回调异常吞掉（CA1031 豁免）。
    /// </summary>
    private static Proto.SupervisorDirective WrapDecider(
        Proto.PID pid, Exception reason, string instanceName, string domainPrefix,
        Action<SupervisionDecision>? onSupervision)
    {
        // CA1031：观测回调可抛任意异常——监督决策必须返回，不容观测面反噬可用性
#pragma warning disable CA1031
        try
        {
            Keystone.Runtime.Trace.KeystoneMeter.SupervisionRestarts.Add(
                1, [new("instance", instanceName)]);
            onSupervision?.Invoke(new SupervisionDecision(
                instanceName, reason.GetBaseException(), nameof(Proto.SupervisorDirective.Restart)));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[keystone] supervision observation hook failed: {ex.Message}");
        }
#pragma warning restore CA1031

        return Proto.SupervisorDirective.Restart; // DC-4：崩溃重启（默认）
    }

    /// <summary>跨域请求（等待响应；TaskId 贯穿，06 §1）。</summary>
    public Task<TaskResultEnvelope> RequestAsync(CapabilityHandle handle, TaskEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handle);
        return RequestCoreAsync(handle.Pid, envelope, cancellationToken);
    }

    /// <summary>
    /// 管道原子替换（DC-10，ADR-0003 决策 2 / 04 §8）：新中间件链 → actor 内构建新管道 →
    /// 原子换引用。保留 actor/context（状态不丢），只换管道链；串行循环内在途请求走旧链完成后生效。
    /// </summary>
    public Task SwapPipelineAsync(CapabilityHandle handle, IReadOnlyList<IMiddleware> middlewares)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(middlewares);
        _system.Root.Send(handle.Pid, new SwapPipeline(middlewares));
        return Task.CompletedTask;
    }

    internal async Task<TaskResultEnvelope> RequestCoreAsync(PID pid, TaskEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pid);
        ArgumentNullException.ThrowIfNull(envelope);

        var response = await _system.Root
            .RequestAsync<DomainResponse>(pid, new DomainRequest(envelope, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
        return response.Envelope;
    }

    /// <summary>释放内部 ActorSystem（仅 Create 模式拥有；Attach 模式由调用方管理）。</summary>
    public async ValueTask DisposeAsync()
    {
        if (_ownsSystem)
        {
            await _system.ShutdownAsync().ConfigureAwait(false);

            // P70-T5（ADR-0018 L2）：实例停止事实入审计流（与 ActorRestartedFact 对称）——
            // 显式 eventStore 的实例逐个落盘（Durable=false 尽力写，不阻塞关闭）
            foreach (var (name, store) in _spawned)
            {
                if (store is not null)
                {
                    await new EventBus(store)
                        .EmitAsync(new ActorStoppedFact(name)).ConfigureAwait(false);
                }
            }
        }
    }
}
