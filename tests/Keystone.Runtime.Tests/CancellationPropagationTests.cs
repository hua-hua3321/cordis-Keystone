using Keystone.Core.Contracts;
using Keystone.Runtime.Actors;
using Keystone.Runtime.Context;
using Keystone.Runtime.Pipeline;
using Proto;

namespace Keystone.Runtime.Tests;

/// <summary>
/// DC-14（17-doc-compliance-audit / 06 §1）：取消贯穿全链——CT 传中间件/handler。
/// 修复前：取消止于传输层（RequestAsync CT 只取消等待，actor 内 handler 照跑、中间件看不到）。
/// 兑现：调用方 CT 随 DomainRequest 进入 actor → 实例 context 暴露 IPluginContext.CancellationToken
/// （中间件/handler 闭包可读）；已取消请求 fail-fast 返回 PipelineCancelled 结果；
/// 中间件内抛 OperationCanceledException → 任务失败（不升级监督重启）。
/// </summary>
public class CancellationPropagationTests
{
    private static TaskEnvelope Envelope(Guid taskId) => new()
    {
        TaskId = taskId,
        Capability = "fs",
        Operation = "read",
        PayloadBytes = [],
    };

    private static TaskResultEnvelope Ok(TaskEnvelope e) => new()
    {
        TaskId = e.TaskId,
        Succeeded = true,
        Type = TaskResultType.Completed,
    };

    [Fact]
    public async Task Middleware_observes_caller_cancellation_token()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var system = new ActorSystem();
        var domain = CapabilityDomain.Attach(system, "fs");
        CancellationToken? observed = null;

        var handle = domain.Spawn("fs-a", e => Task.FromResult(Ok(e)),
            [new TokenCapturingMiddleware(ctx => observed = ctx.CancellationToken)]);

        var result = await domain.RequestAsync(handle, Envelope(Guid.NewGuid()), cts.Token);

        Assert.True(result.Succeeded);
        Assert.Equal(cts.Token, observed); // 中间件读到调用方 CT（06 §1 贯穿）
    }

    private sealed class TokenCapturingMiddleware(Action<IPluginContext> capture) : IMiddleware
    {
        public string Id => "token-capture";

        public int Order => 0;

        public async Task InvokeAsync(IPluginContext ctx, RequestDelegate next)
        {
            capture(ctx);
            await next(ctx);
        }
    }

    [Fact]
    public async Task Already_canceled_request_fails_fast_without_execution()
    {
        await using var system = new ActorSystem();
        var domain = CapabilityDomain.Attach(system, "fs");
        var executions = 0;

        var handle = domain.Spawn("fs-a", e =>
        {
            executions++;
            return Task.FromResult(Ok(e));
        });

        // 已取消 CT 直达 actor（传输层 Proto.Actor 对已取消 token 抛 ArgumentException——
        // 拒于发送；actor 侧语义经原始投递验证）：fail-fast 不执行 handler
        handle.SendRaw(new DomainRequest(Envelope(Guid.NewGuid()), new CancellationToken(canceled: true)), system);
        await Task.Delay(200);

        Assert.Equal(0, executions); // handler 未执行（fail-fast）
    }

    [Fact]
    public async Task Canceled_request_returns_failed_envelope_with_pipeline_cancelled()
    {
        // actor 侧语义：已取消 → 记录 PipelineCancelled 失败结果（非异常升级——不触发监督重启）
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var system = new ActorSystem();
        var domain = CapabilityDomain.Attach(system, "fs");
        var executions = 0;
        var taskId = Guid.NewGuid();

        var handle = domain.Spawn("fs-a", e =>
        {
            executions++;
            return Task.FromResult(Ok(e));
        });
        // 预热：确认 actor 存活
        await domain.RequestAsync(handle, Envelope(Guid.NewGuid()), cts.Token);

        // 直接投递已取消的 DomainRequest（绕过 Proto 传输层取消——模拟取消后消息仍达）
        handle.SendRaw(new DomainRequest(Envelope(taskId), new CancellationToken(canceled: true)), system);
        await Task.Delay(200);

        // 再以正常 CT 请求同 TaskId：幂等缓存（DC-13）返回已记录的 PipelineCancelled 结果
        var result = await domain.RequestAsync(handle, Envelope(taskId), cts.Token);

        Assert.Equal(1, executions); // handler 仅预热执行一次（取消请求 fail-fast）
        Assert.False(result.Succeeded);
        Assert.Equal(Keystone.Core.Errors.ErrorCode.PipelineCancelled, result.ErrorCode);
    }

    [Fact]
    public async Task Cancellation_flows_down_context_chain_to_plugin_handlers()
    {
        // 06 §1 CT 传 handler：插件 handler 闭包持有自己的 context（实例 context 的子链）——
        // CT 未在自身槽设置时沿链向上取实例 context 的请求 CT
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var instance = new ContextFacade("fs-a");
        var pluginScope = new ContextFacade("plugin", parent: instance);
        CancellationToken handlerToken = CancellationToken.None;

        instance.SetRequestCancellationToken(cts.Token); // actor 每请求设置（串行循环内）
        handlerToken = pluginScope.CancellationToken;    // handler 经链读得（闭包模拟）

        Assert.Equal(cts.Token, handlerToken);

        instance.SetRequestCancellationToken(CancellationToken.None); // 请求结束复位
        Assert.Equal(CancellationToken.None, pluginScope.CancellationToken);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Middleware_cancellation_exception_yields_failed_envelope()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var system = new ActorSystem();
        var domain = CapabilityDomain.Attach(system, "fs");
        var taskId = Guid.NewGuid();

        var handle = domain.Spawn("fs-a", e => Task.FromResult(Ok(e)),
            [new CancelingMiddleware()]);

        var result = await domain.RequestAsync(handle, Envelope(taskId), cts.Token);

        // 中间件取消 → PipelineCancelled 失败（不升级异常触发监督重启）
        Assert.False(result.Succeeded);
        Assert.Equal(Keystone.Core.Errors.ErrorCode.PipelineCancelled, result.ErrorCode);
    }

    private sealed class CancelingMiddleware : IMiddleware
    {
        public string Id => "cancel";

        public int Order => 0;

        public Task InvokeAsync(IPluginContext ctx, RequestDelegate next)
            => throw new OperationCanceledException("caller canceled");
    }
}
