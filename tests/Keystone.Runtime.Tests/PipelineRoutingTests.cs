using Keystone.Runtime.Context;
using Keystone.Runtime.Pipeline;

namespace Keystone.Runtime.Tests;

public sealed record OperationRequested(string Capability, string Operation);

/// <summary>
/// 双轨路由测试（04 §3）：管道插件（中间件）走管道；决策插件（serial/bail）
/// 与观察者插件（parallel/emit）走事件轨——三类路由独立正确。
/// </summary>
public class PipelineRoutingTests
{
    [Fact]
    public async Task Three_tracks_route_independently()
    {
        var ctx = new ContextFacade("domain");
        var order = new List<string>();

        // 管道轨：中间件包裹
        var builder = new PipelineBuilder();
        builder.AddMiddleware(new LoggingMiddleware(order));
        builder.SetTerminal(_ =>
        {
            order.Add("executor");
            return Task.CompletedTask;
        });
        var pipeline = builder.Build();

        // 决策轨：serial 首个决策生效
        ctx.SubscribeSerial<OperationRequested>(e =>
        {
            order.Add($"decision:{e.Operation}");
            return Task.FromResult<object?>(e.Operation == "read" ? "allowed" : null);
        });

        // 观察轨：parallel 不干预
        ctx.SubscribeParallel<OperationRequested>(e =>
        {
            order.Add($"observe:{e.Operation}");
            return Task.CompletedTask;
        });

        // 三类各自触发
        await pipeline.InvokeAsync(ctx);
        var decision = await ctx.Events.PublishSerialAsync(new OperationRequested("fs", "read"), ctx);
        await ctx.Events.PublishParallelAsync(new OperationRequested("fs", "read"), ctx); // 观察者走 parallel

        Assert.Equal(["middleware-before", "executor", "middleware-after"], order.Take(3)); // 管道独立执行
        Assert.Contains("decision:read", order);   // 决策轨生效
        Assert.Equal("allowed", decision);
        Assert.Contains("observe:read", order);    // 观察轨独立
    }

    [Fact]
    public async Task Decision_track_short_circuits_but_observer_track_does_not()
    {
        var ctx = new ContextFacade("domain");
        var order = new List<string>();

        ctx.SubscribeSerial<OperationRequested>(e =>
        {
            order.Add("decision-1");
            return Task.FromResult<object?>("decided");
        });
        ctx.SubscribeSerial<OperationRequested>(e =>
        {
            order.Add("decision-2"); // 首个决策后不执行（bail 语义）
            return Task.FromResult<object?>(null);
        });
        ctx.SubscribeParallel<OperationRequested>(e =>
        {
            order.Add("observer");
            return Task.CompletedTask;
        });

        var result = await ctx.Events.PublishSerialAsync(new OperationRequested("fs", "read"), ctx);
        await ctx.Events.PublishParallelAsync(new OperationRequested("fs", "read"), ctx);

        Assert.Equal("decided", result);
        Assert.DoesNotContain("decision-2", order); // serial 短路
        Assert.Contains("observer", order);         // 观察者不受决策短路影响
    }

    private sealed class LoggingMiddleware : IMiddleware
    {
        private readonly List<string> _order;

        public LoggingMiddleware(List<string> order)
        {
            _order = order;
        }

        public string Id => "logging";

        public int Order => 0;

        public async Task InvokeAsync(IPluginContext ctx, RequestDelegate next)
        {
            _order.Add("middleware-before");
            await next(ctx);
            _order.Add("middleware-after");
        }
    }
}
