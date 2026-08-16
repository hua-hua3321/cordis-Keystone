using System.Diagnostics.Metrics;

namespace Keystone.Runtime.Trace;

/// <summary>
/// Runtime 观测指标 hub（P70-T3，ADR-0018 L1）：Meter "Keystone.Runtime"——纯 BCL，
/// 与 <see cref="TraceContext"/> 同名源归属。宿主经 OTel AddMeter("Keystone.Runtime")
/// 订阅导出；无 listener 时计数近零开销。tag 维度对齐 span/log 字段命名
/// （capability/instance/faultType）——一次查询 TaskId/instance 同时命中三面。
/// </summary>
public static class KeystoneMeter
{
    public const string Name = "Keystone.Runtime";

    private static readonly Meter Meter = new(Name);

    /// <summary>actor 消息处理计数（维度：capability / instance）。</summary>
    public static Counter<long> ActorRequests { get; } = Meter.CreateCounter<long>(
        "keystone.actor.requests", unit: "{request}", description: "capability domain requests processed");

    /// <summary>actor 消息处理时长直方图 ms（维度：capability）。</summary>
    public static Histogram<double> ActorRequestDuration { get; } = Meter.CreateHistogram<double>(
        "keystone.actor.request_duration", unit: "ms", description: "capability domain request duration");

    /// <summary>管道/handler 故障计数（维度：instance / faultType——P68 归因：pipeline|handler）。</summary>
    public static Counter<long> ActorFaults { get; } = Meter.CreateCounter<long>(
        "keystone.actor.faults", unit: "{fault}", description: "pipeline or terminal handler faults");

    /// <summary>慢请求计数（维度：capability；伴随 warn 日志——无超时挂起形态的第一道防线）。</summary>
    public static Counter<long> SlowRequests { get; } = Meter.CreateCounter<long>(
        "keystone.slow_requests", unit: "{request}", description: "requests exceeding slow threshold");

    /// <summary>监督重启计数（维度：instance；decider 内递增——05 §2 重启计数的指标面）。</summary>
    public static Counter<long> SupervisionRestarts { get; } = Meter.CreateCounter<long>(
        "keystone.supervision.restarts", unit: "{restart}", description: "actor supervision restarts");

    /// <summary>热更通道计数（维度：channel=hot|cold；P70-T4 接线）。</summary>
    public static Counter<long> HotUpdateOperations { get; } = Meter.CreateCounter<long>(
        "keystone.hotupdate.operations", unit: "{operation}", description: "hot update channel usage");

    /// <summary>写回失败计数（OnWriteFailed 同源；P70-T4 接线）。</summary>
    public static Counter<long> WriterFailures { get; } = Meter.CreateCounter<long>(
        "keystone.writer.failures", unit: "{failure}", description: "config write-back failures");
}
