using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Keystone.Core.Contracts;
using Keystone.Runtime.Actors;
using Keystone.Runtime.Context;
using Keystone.Runtime.Trace;

namespace Keystone.Runtime.Tests;

/// <summary>
/// P70-T3（ADR-0018）：actor 边界观测三面——结构化日志（进 Debug/出 Information 含耗时）+
/// meter（requests/duration/faults/slow）+ 监督（restarts 计数 + 回调）。
/// MeterListener/日志捕获为进程级全局——本 collection 非并行；断言用唯一 capability 标记
/// 防并行 assembly 串扰。
/// </summary>
[Collection("Observability")]
public class ActorObservabilityTests
{
    private const string MarkerCapability = "obs-t3";

    private static TaskResultEnvelope Ok(TaskEnvelope e) => new()
    {
        TaskId = e.TaskId,
        Succeeded = true,
        Type = TaskResultType.Completed,
    };

    private static TaskEnvelope Envelope(string op) => new()
    {
        TaskId = Guid.NewGuid(),
        Capability = MarkerCapability,
        Operation = op,
        PayloadBytes = [],
    };

    // ── meter 面 ──

    [Fact]
    public async Task Request_completion_records_requests_and_duration()
    {
        var requests = new List<(string Instrument, double Value)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (i, l) =>
            {
                if (i.Meter.Name == KeystoneMeter.Name)
                {
                    l.EnableMeasurementEvents(i);
                }
            },
        };
        listener.SetMeasurementEventCallback<double>((i, v, _, _) => requests.Add((i.Name, v)));
        listener.SetMeasurementEventCallback<long>((i, v, _, _) => requests.Add((i.Name, v)));
        listener.Start();

        await using var system = new Proto.ActorSystem();
        var domain = CapabilityDomain.Attach(system, "obs");
        var handle = domain.Spawn("m1", e => Task.FromResult(Ok(e)));
        var result = await domain.RequestAsync(handle, Envelope("count"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains(requests, r => r.Instrument == "keystone.actor.requests" && r.Value == 1);
        Assert.Contains(requests, r => r.Instrument == "keystone.actor.request_duration" && r.Value >= 0);
    }

    [Fact]
    public async Task Slow_request_increments_slow_counter()
    {
        var slows = 0;
        using var listener = new MeterListener
        {
            InstrumentPublished = (i, l) =>
            {
                if (i.Meter.Name == KeystoneMeter.Name)
                {
                    l.EnableMeasurementEvents(i);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((i, v, _, _) =>
        {
            if (i.Name == "keystone.slow_requests")
            {
                slows++;
            }
        });
        listener.Start();

        await using var system = new Proto.ActorSystem();
        var domain = CapabilityDomain.Attach(system, "obs");
        // 域默认慢阈值 5s 太长——Spawn 显式给 10ms，handler 睡 60ms → 必超
        var handle = domain.Spawn(
            "m2",
            async e =>
            {
                await Task.Delay(60, CancellationToken.None);
                return Ok(e);
            },
            slowRequestThreshold: TimeSpan.FromMilliseconds(10));
        await domain.RequestAsync(handle, Envelope("slow"), CancellationToken.None);

        Assert.True(slows >= 1);
    }

    [Fact]
    public async Task Middleware_fault_increments_fault_counter_with_type()
    {
        var faults = new List<string>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (i, l) =>
            {
                if (i.Meter.Name == KeystoneMeter.Name && i.Name == "keystone.actor.faults")
                {
                    l.EnableMeasurementEvents(i);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            faults.Add(tags.ToArray().FirstOrDefault(t => t.Key == "faultType").Value?.ToString() ?? "?"));
        listener.Start();

        await using var system = new Proto.ActorSystem();
        var domain = CapabilityDomain.Attach(system, "obs");
        var handle = domain.Spawn(
            "m3",
            e => Task.FromResult(Ok(e)),
            [new ThrowingMiddleware()]);
        var result = await domain.RequestAsync(handle, Envelope("fault"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("pipeline", faults);
    }

    private sealed class ThrowingMiddleware : Keystone.Runtime.Pipeline.IMiddleware
    {
        public string Id => "throwing";

        public int Order => 0;

        public Task InvokeAsync(IPluginContext ctx, Keystone.Runtime.Pipeline.RequestDelegate next)
            => throw new Keystone.Core.Errors.KeystoneException(
                Keystone.Core.Errors.ErrorCode.PipelineExecutionFailed, "boom");
    }

    // ── 监督面 ──

    [Fact]
    public async Task Handler_crash_fires_supervision_callback_and_restart_counter()
    {
        var decisions = new List<CapabilityDomain.SupervisionDecision>();
        var restarts = 0;
        using var listener = new MeterListener
        {
            InstrumentPublished = (i, l) =>
            {
                if (i.Meter.Name == KeystoneMeter.Name && i.Name == "keystone.supervision.restarts")
                {
                    l.EnableMeasurementEvents(i);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, _, _, _) => restarts++);
        listener.Start();

        await using var system = new Proto.ActorSystem();
        var domain = CapabilityDomain.Attach(
            system, "obs", onSupervision: d => decisions.Add(d));
        var calls = 0;
        var handle = domain.Spawn("m4", e =>
        {
            Interlocked.Increment(ref calls);
            throw new InvalidOperationException("supervised");
#pragma warning disable CS0162 // 不可达返回——lambda 形态需要
            return Task.FromResult(Ok(e));
#pragma warning restore CS0162
        });

        try
        {
            await domain.RequestAsync(handle, Envelope("crash"), CancellationToken.None);
        }
        catch (Exception)
        {
            // P68 语义：handler 崩溃回填失败结果后上抛（监督重启）；此处两种形态都接受
        }

        // 监督在 respond 之后异步发生——轮询等待决策落地（上限 2s）
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (decisions.Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20, CancellationToken.None);
        }

        Assert.Contains(decisions, d =>
            d.InstanceName == "m4" &&
            d.Directive == "Restart" &&
            d.Reason.Message == "supervised");
        Assert.True(restarts >= 1);
    }

    // ── span 状态面（P70-T5）──

    [Fact]
    public async Task Failed_request_marks_task_span_error_status()
    {
        var spans = new List<Activity>();
        using var listener = SpanCapture(spans);
        ActivitySource.AddActivityListener(listener);

        await using var system = new Proto.ActorSystem();
        var domain = CapabilityDomain.Attach(system, "obs");
        var handle = domain.Spawn("t2-fail", e => Task.FromResult(Ok(e)), [new ThrowingMiddleware()]);
        var result = await domain.RequestAsync(handle, Envelope("fail"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(spans, a => a.OperationName == TraceContext.ActivityName
            && a.Status == ActivityStatusCode.Error);
    }

    [Fact]
    public async Task Succeeded_request_leaves_task_span_without_error_status()
    {
        var spans = new List<Activity>();
        using var listener = SpanCapture(spans);
        ActivitySource.AddActivityListener(listener);

        await using var system = new Proto.ActorSystem();
        var domain = CapabilityDomain.Attach(system, "obs");
        var handle = domain.Spawn("t2-ok", e => Task.FromResult(Ok(e)));
        var result = await domain.RequestAsync(handle, Envelope("ok"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains(spans, a => a.OperationName == TraceContext.ActivityName
            && a.Status != ActivityStatusCode.Error);
    }

    private static ActivityListener SpanCapture(List<Activity> spans)
    {
        var gate = new object();
        return new ActivityListener
        {
            ShouldListenTo = s => s.Name == TraceContext.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = a =>
            {
                lock (gate)
                {
                    spans.Add(a);
                }
            },
        };
    }

    // ── 日志面 ──

    [Fact]
    public async Task Boundary_writes_start_debug_and_completed_information_logs()
    {
        var entries = new List<(LogLevel Level, string Message)>();
        var factory = new CapturingLoggerFactory(entries);
        var root = new ContextFacade("root", loggerFactory: factory);

        await using var system = new Proto.ActorSystem();
        var domain = CapabilityDomain.Attach(system, "obs");
        var handle = domain.Spawn("m5", e => Task.FromResult(Ok(e)), parentContext: root);
        var result = await domain.RequestAsync(handle, Envelope("log"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains(entries, e => e.Level == LogLevel.Debug && e.Message.Contains("start: obs-t3/log"));
        Assert.Contains(entries, e => e.Level == LogLevel.Information && e.Message.Contains("completed: succeeded=True"));
    }

    private sealed class CapturingLoggerFactory(List<(LogLevel, string)> entries)
        : ILoggerFactory
    {
        public void Dispose() { }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(entries);

        public void AddProvider(ILoggerProvider provider) { }

        private sealed class CapturingLogger(List<(LogLevel, string)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
                => entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
