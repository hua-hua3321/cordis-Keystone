using Keystone.Core.Contracts;
using Keystone.Runtime.Context;
using Keystone.Runtime.Pipeline;
using Proto;
using IContext = Proto.IContext;

namespace Keystone.Runtime.Actors;

/// <summary>
/// 能力域 actor（T1 Proto.Actor，01 §2-§4）：串行消息循环（actor 原生，context 无竞争）
/// + 监督重启（Proto.Actor 默认策略）。每次消息 = 一次跨域请求处理。
/// 01 §3/§4（P34，DC-1）：**actor 持实例级持久 context**（与 actor 同生命周期）——
/// 构造时创建、跨请求复用；中间件/请求在其上执行（接入父链服务解析 + 共享事件总线，03 §2）。
/// 01 §2 管道承诺（P22，B3）：actor 持管道——中间件链包裹 handler（terminal）。
/// 15-decoupling-plan D1（C1b）：internal 实现细节——IActor/IContext 不外泄。
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812",
    Justification = "CapabilityActor 由 Proto.Actor Props.FromProducer 反射实例化（CapabilityDomain.Spawn）")]
internal sealed class CapabilityActor : IActor
{
    private readonly Func<TaskEnvelope, Task<TaskResultEnvelope>> _handler;
    private readonly IReadOnlyList<IMiddleware> _middlewares;
    private readonly ContextFacade _instanceContext;

    public CapabilityActor(
        string instanceName,
        Func<TaskEnvelope, Task<TaskResultEnvelope>> handler,
        IReadOnlyList<IMiddleware>? middlewares = null,
        Keystone.Runtime.Context.IContext? parentContext = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
        _middlewares = middlewares ?? [];
        // 01 §4：每实例独立持久 context（父 = 宿主 root，接入插件服务链 + 共享事件总线 ID-08）
        _instanceContext = new ContextFacade(instanceName, parentContext);
    }

    /// <summary>实例级 context（01 §4：actor=context 同生命周期；测试/诊断/宿主可访问）。</summary>
    internal ContextFacade InstanceContext => _instanceContext;

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

        // terminal：handler 结果写入结果槽（envelope 闭包捕获）；在实例级持久 context 上执行管道
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
        await pipeline.InvokeAsync(_instanceContext).ConfigureAwait(false);
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
