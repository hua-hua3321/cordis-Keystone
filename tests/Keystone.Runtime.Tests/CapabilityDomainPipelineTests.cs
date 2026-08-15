using Keystone.Core.Contracts;
using Keystone.Runtime.Actors;
using Keystone.Runtime.Context;
using Keystone.Runtime.Pipeline;
using Proto;

namespace Keystone.Runtime.Tests;

/// <summary>
/// 能力域管道测试（P22，B3）：01 §2"actor 持管道"——插件中间件包裹跨域请求 handler。
/// before/after 顺序、短路语义（ADR-0006 waterfall）。
/// </summary>
public class CapabilityDomainPipelineTests
{
    private sealed class RecordingMiddleware(string id, int order, List<string> trace, bool shortCircuit = false) : IMiddleware
    {
        public string Id => id;

        public int Order => order;

        public async Task InvokeAsync(IPluginContext ctx, RequestDelegate next)
        {
            trace.Add($"{id}:before");
            if (shortCircuit)
            {
                return; // 不调 next → 短路（终端不执行）
            }

            await next(ctx);
            trace.Add($"{id}:after");
        }
    }

    [Fact]
    public async Task Middlewares_wrap_handler_in_order()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var system = new ActorSystem();
        var domain = CapabilityDomain.Attach(system, "fs");
        var trace = new List<string>();

        var handle = domain.Spawn(
            "fs-a",
            envelope => Task.FromResult(new TaskResultEnvelope
            {
                TaskId = envelope.TaskId,
                Succeeded = true,
                Type = TaskResultType.Completed,
            }),
            new IMiddleware[]
            {
                new RecordingMiddleware("audit", 1, trace),
                new RecordingMiddleware("rate-limit", 2, trace),
            });

        var result = await domain.RequestAsync(handle, new TaskEnvelope
        {
            TaskId = Guid.NewGuid(),
            Capability = "fs",
            Operation = "read",
            PayloadBytes = [],
        }, cts.Token);

        Assert.True(result.Succeeded);
        Assert.Equal(["audit:before", "rate-limit:before", "rate-limit:after", "audit:after"], trace);
    }

    [Fact]
    public async Task Middleware_short_circuit_skips_handler()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var system = new ActorSystem();
        var domain = CapabilityDomain.Attach(system, "fs");
        var trace = new List<string>();
        var handlerCalled = false;

        var handle = domain.Spawn(
            "fs-a",
            envelope =>
            {
                handlerCalled = true;
                return Task.FromResult(new TaskResultEnvelope
                {
                    TaskId = envelope.TaskId,
                    Succeeded = true,
                    Type = TaskResultType.Completed,
                });
            },
            new IMiddleware[] { new RecordingMiddleware("blocker", 1, trace, shortCircuit: true) });

        // 短路：无终端结果 → 请求失败（终端未执行）
        var result = await domain.RequestAsync(handle, new TaskEnvelope
        {
            TaskId = Guid.NewGuid(),
            Capability = "fs",
            Operation = "read",
            PayloadBytes = [],
        }, cts.Token);

        Assert.False(handlerCalled); // handler 未执行（短路）
        Assert.False(result.Succeeded);
        Assert.Equal(["blocker:before"], trace);
    }
}
