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
/// DC-10（ADR-0003 决策 2 / 04 §8）：管道**实例化缓存**（构建一次跨请求复用）+ **swap 原子替换**
/// （新链构建后换引用；保留 actor/context，只换管道链；串行循环保证无半新半旧交错）。
/// 15-decoupling-plan D1（C1b）：internal 实现细节——IActor/IContext 不外泄。
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812",
    Justification = "CapabilityActor 由 Proto.Actor Props.FromProducer 反射实例化（CapabilityDomain.Spawn）")]
internal sealed class CapabilityActor : IActor
{
    private readonly Func<TaskEnvelope, Task<TaskResultEnvelope>> _handler;
    private readonly ContextFacade _instanceContext;

    // DC-10：缓存管道 + 当前请求槽（actor 串行循环内无竞争）
    private volatile IPipeline _pipeline;
    private TaskEnvelope? _currentEnvelope;
    private TaskResultEnvelope? _currentResult;

    // DC-13（06 §4 幂等契约）：TaskId 即幂等键——重复投递/重试返回缓存结果（不重执行副作用）
    private const int ResultCacheCapacity = 1024;
    private readonly Dictionary<Guid, TaskResultEnvelope> _results = [];
    private readonly Queue<Guid> _resultOrder = new();

    public CapabilityActor(
        string instanceName,
        Func<TaskEnvelope, Task<TaskResultEnvelope>> handler,
        IReadOnlyList<IMiddleware>? middlewares = null,
        Keystone.Runtime.Context.IContext? parentContext = null,
        Keystone.Runtime.Persistence.IEventStore? eventStore = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
        InstanceName = instanceName;
        // 01 §4：每实例独立持久 context（父 = 宿主 root，接入插件服务链 + 共享事件总线 ID-08）
        // DC-11：独立实例的总线携带事实存储（有父总线时共享父的）
        _instanceContext = new ContextFacade(instanceName, parentContext, eventStore: eventStore);
        // DC-10：构建一次缓存（无中间件 = 直通，语义与直调 handler 一致）
        _pipeline = BuildPipeline(middlewares ?? []);
    }

    /// <summary>实例名（事实事件 Capability 维度）。</summary>
    internal string InstanceName { get; }

    /// <summary>实例级 context（01 §4：actor=context 同生命周期；测试/诊断/宿主可访问）。</summary>
    internal ContextFacade InstanceContext => _instanceContext;

    public async Task ReceiveAsync(IContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        switch (context.Message)
        {
            case DomainRequest { Envelope: var envelope, CancellationToken: var requestCt }:
                // DC-13：幂等去重——重复 TaskId 直接回缓存（不重执行、不重发事实）
                if (_results.TryGetValue(envelope.TaskId, out var cached))
                {
                    context.Respond(new DomainResponse(cached));
                    break;
                }

                // DC-14：已取消请求 fail-fast——不执行 handler（06 §1 取消贯穿；记录失败非异常升级）
                TaskResultEnvelope result;
                if (requestCt.IsCancellationRequested)
                {
                    result = CancelledResult(envelope);
                }
                else
                {
                    // DC-14：请求 CT 经实例 context 暴露（中间件/handler 链上可读；结束复位）
                    _instanceContext.SetRequestCancellationToken(requestCt);
                    try
                    {
                        result = await ExecuteTracedAsync(envelope).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // 中间件/handler 主动取消 → 任务失败（不升级监督重启）
                        result = CancelledResult(envelope);
                    }
                    finally
                    {
                        _instanceContext.SetRequestCancellationToken(CancellationToken.None);
                    }
                }

                RecordResult(envelope.TaskId, result);
                await PublishFactAsync(envelope, result).ConfigureAwait(false); // DC-11：任务完成/失败事实
                context.Respond(new DomainResponse(result));
                break;

            case SwapPipeline { Middlewares: var middlewares }:
                // DC-10：原子替换——新链构建完成后换引用；串行循环内在途请求已捕获旧链（无交错）
                _pipeline = BuildPipeline(middlewares);
                break;
        }
    }

    /// <summary>
    /// DC-10：构建管道并缓存。terminal 经 actor 当前请求槽路由 handler（链缓存跨请求复用，
    /// 每请求只换槽内容）；在实例级持久 context 上执行。
    /// </summary>
    private IPipeline BuildPipeline(IReadOnlyList<IMiddleware> middlewares)
    {
        if (middlewares.Count == 0)
        {
            return new DirectPipeline(_handler);
        }

        var builder = new PipelineBuilder();
        foreach (var middleware in middlewares)
        {
            builder.AddMiddleware(middleware);
        }

        builder.SetTerminal(_ =>
        {
            _currentResult = _handler(_currentEnvelope!).GetAwaiter().GetResult();
            return Task.CompletedTask;
        });

        return builder.Build();
    }

    /// <summary>
    /// DC-13（05 §5/06 §3）：请求执行包裹 keystone.task Activity——TaskId/ParentTaskId/能力域/操作
    /// tag 贯穿（中间件/服务内读 Activity.Current 即得，H1）；结束恢复前序 Activity。
    /// </summary>
    private async Task<TaskResultEnvelope> ExecuteTracedAsync(TaskEnvelope envelope)
    {
        var activity = Keystone.Runtime.Trace.TraceContext.StartTask(
            new TaskId(envelope.TaskId),
            envelope.Capability ?? "unknown",
            envelope.Operation ?? "unknown",
            envelope.ParentTaskId is { } parent ? new TaskId(parent) : null);
        try
        {
            return await ExecuteAsync(envelope).ConfigureAwait(false);
        }
        finally
        {
            activity.Dispose();
        }
    }

    /// <summary>DC-14：取消结果（PipelineCancelled——06 §1 取消贯穿；失败不升级监督）。</summary>
    private static TaskResultEnvelope CancelledResult(TaskEnvelope envelope) => new()
    {
        TaskId = envelope.TaskId,
        Succeeded = false,
        Type = TaskResultType.Failed,
        ErrorCode = Keystone.Core.Errors.ErrorCode.PipelineCancelled,
        ErrorDetail = "request canceled before completion (caller CT)",
    };

    /// <summary>DC-13：结果缓存（FIFO 容量上限，防无界增长）。</summary>
    private void RecordResult(Guid taskId, TaskResultEnvelope result)
    {
        if (_results.Count >= ResultCacheCapacity && _results.ContainsKey(taskId) is false)
        {
            var oldest = _resultOrder.Dequeue();
            _results.Remove(oldest);
        }

        if (_results.TryAdd(taskId, result))
        {
            _resultOrder.Enqueue(taskId);
        }
    }

    /// <summary>经管道执行跨域请求（DC-10：缓存管道；无中间件直通——兼容原语义）。</summary>
    private async Task<TaskResultEnvelope> ExecuteAsync(TaskEnvelope envelope)
    {
        if (_pipeline is DirectPipeline direct)
        {
            return await direct.ExecuteAsync(envelope).ConfigureAwait(false);
        }

        _currentEnvelope = envelope;
        _currentResult = null;
        await _pipeline.InvokeAsync(_instanceContext).ConfigureAwait(false);
        return _currentResult
            ?? new TaskResultEnvelope
            {
                TaskId = envelope.TaskId,
                Succeeded = false,
                Type = TaskResultType.Failed,
                ErrorCode = Keystone.Core.Errors.ErrorCode.PipelineMiddlewareRejected,
                ErrorDetail = "pipeline short-circuited before terminal (waterfall 否决)",
            };
    }

    /// <summary>DC-11（04 §7/03 §4）：任务结果事实——emit 经实例总线（携带存储时持久化，尽力写）。</summary>
    private Task PublishFactAsync(TaskEnvelope envelope, TaskResultEnvelope result)
        => result.Succeeded
            ? _instanceContext.Events.EmitAsync(
                new Keystone.Runtime.Events.TaskCompletedFact(envelope.TaskId, InstanceName), _instanceContext)
            : _instanceContext.Events.EmitAsync(
                new Keystone.Runtime.Events.TaskFailedFact(envelope.TaskId, InstanceName, result.ErrorCode), _instanceContext);

    /// <summary>直通管道（无中间件；DC-10 缓存形态——避免无谓包装）。</summary>
    private sealed class DirectPipeline(Func<TaskEnvelope, Task<TaskResultEnvelope>> handler) : IPipeline
    {
        public Task<TaskResultEnvelope> ExecuteAsync(TaskEnvelope envelope) => handler(envelope);

        Task IPipeline.InvokeAsync(IPluginContext context) => throw new NotSupportedException(
            "direct pipeline executes via ExecuteAsync (handler terminal inlined)");
    }
}
