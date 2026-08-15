using Keystone.Core.Contracts;
using Keystone.Runtime.Context;
using Keystone.Runtime.Pipeline;
using Proto;
using IContext = Proto.IContext;

namespace Keystone.Runtime.Actors;

/// <summary>
/// 能力域 actor（T1 Proto.Actor，01 §2-§3）：串行消息循环（actor 原生，context 无竞争）
/// + 监督重启（Proto.Actor 默认策略）。每次消息 = 一次跨域请求处理。
/// 01 §2 管道承诺（P22，B3）：actor 持管道——中间件链包裹 handler（terminal），
/// 中间件 = 插件（IMiddleware，形状 A）；短路/after 语义（ADR-0006 waterfall）。
/// 15-decoupling-plan D1（C1b）：internal 实现细节——IActor/IContext 不外泄。
/// </summary>
internal sealed class CapabilityActor : IActor
{
    private readonly Func<TaskEnvelope, Task<TaskResultEnvelope>> _handler;
    private readonly IReadOnlyList<IMiddleware> _middlewares;
    private readonly string _instanceName;

    public CapabilityActor(
        string instanceName,
        Func<TaskEnvelope, Task<TaskResultEnvelope>> handler,
        IReadOnlyList<IMiddleware>? middlewares = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        ArgumentNullException.ThrowIfNull(handler);
        _instanceName = instanceName;
        _handler = handler;
        _middlewares = middlewares ?? [];
    }

    public async Task ReceiveAsync(IContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        switch (context.Message)
        {
            case DomainRequest { Envelope: var envelope }:
                var result = await ExecuteAsync(envelope).ConfigureAwait(false);
                context.Respond(new DomainResponse(result));
                break;
        }
    }

    /// <summary>经中间件管道执行跨域请求（无中间件时直接调 handler——兼容原语义）。</summary>
    private async Task<TaskResultEnvelope> ExecuteAsync(TaskEnvelope envelope)
    {
        if (_middlewares.Count == 0)
        {
            return await _handler(envelope).ConfigureAwait(false);
        }

        // 请求级 context（能力域实例隔离：独立 context；中间件经它拿服务/日志/事件）
        var requestContext = new ContextFacade($"{_instanceName}-req");

        // terminal：handler 结果写入请求级结果槽（envelope 闭包捕获）
        TaskResultEnvelope? result = null;
        var builder = new PipelineBuilder();
        foreach (var middleware in _middlewares)
        {
            builder.AddMiddleware(middleware);
        }

        builder.SetTerminal(_ =>
        {
            result = _handler(envelope).GetAwaiter().GetResult();
            return Task.CompletedTask;
        });

        var pipeline = builder.Build();
        await pipeline.InvokeAsync(requestContext).ConfigureAwait(false);
        return result
            ?? new TaskResultEnvelope
            {
                TaskId = envelope.TaskId,
                Succeeded = false,
                Type = TaskResultType.Failed,
                ErrorCode = Keystone.Core.Errors.ErrorCode.PipelineMiddlewareRejected,
                ErrorDetail = "pipeline short-circuited before terminal (waterfall 否决)",
            };
    }
}
