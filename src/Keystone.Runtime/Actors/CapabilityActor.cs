using Keystone.Core.Contracts;
using Microsoft.Extensions.Logging;
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
internal sealed partial class CapabilityActor : IActor
{
    private readonly Func<TaskEnvelope, Task<TaskResultEnvelope>> _handler;
    private readonly ContextFacade _instanceContext;

    // DC-10：缓存管道 + 当前请求槽（actor 串行循环内无竞争）
    private volatile IPipeline _pipeline;
    private TaskEnvelope? _currentEnvelope;
    private TaskResultEnvelope? _currentResult;

    // DC-13（06 §4 幂等契约）：TaskId 即幂等键——重复投递/重试返回缓存结果（不重执行副作用）
    /// <summary>DC-13 结果缓存容量默认值（P71-T2 入构造参数——历史硬编码常量 1024）。</summary>
    private const int DefaultResultCacheCapacity = 1024;
    private readonly Dictionary<Guid, TaskResultEnvelope> _results = [];
    private readonly Queue<Guid> _resultOrder = new();

    public CapabilityActor(
        string instanceName,
        Func<TaskEnvelope, Task<TaskResultEnvelope>> handler,
        IReadOnlyList<IMiddleware>? middlewares = null,
        Keystone.Runtime.Context.IContext? parentContext = null,
        Keystone.Runtime.Persistence.IEventStore? eventStore = null,
        TimeSpan? slowRequestThreshold = null,
        int? resultCacheCapacity = null)
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
        // P70-T3：慢请求阈值（宿主 Observability 下传；null = 框架默认 5s）
        _slowRequestThreshold = slowRequestThreshold ?? TimeSpan.FromSeconds(5);
        // P71-T2：结果缓存容量（宿主配置下传；null = 框架默认 1024）
        _resultCacheCapacity = resultCacheCapacity ?? DefaultResultCacheCapacity;
    }

    private readonly TimeSpan _slowRequestThreshold;
    private readonly int _resultCacheCapacity;

    /// <summary>实例名（事实事件 Capability 维度）。</summary>
    internal string InstanceName { get; }

    /// <summary>实例级 context（01 §4：actor=context 同生命周期；测试/诊断/宿主可访问）。</summary>
    internal ContextFacade InstanceContext => _instanceContext;

    public async Task ReceiveAsync(IContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        switch (context.Message)
        {
            case DomainRequest request:
                await HandleDomainRequestAsync(context, request).ConfigureAwait(false);
                break;

            case SwapPipeline { Middlewares: var middlewares }:
                // DC-10：原子替换——新链构建完成后换引用；串行循环内在途请求已捕获旧链（无交错）
                _pipeline = BuildPipeline(middlewares);
                break;
        }
    }

    /// <summary>
    /// DomainRequest 处理（P70-T3 边界观测）：消息模型调试性三面在此汇合——
    /// 结构化日志（进 Debug/出 Information 含耗时）+ meter（requests/duration/slow）+ 既有事实。
    /// 缓存命中/取消路径不走完整观测（非执行面）。
    /// </summary>
    private async Task HandleDomainRequestAsync(IContext context, DomainRequest request)
    {
        var envelope = request.Envelope;
        // DC-13：幂等去重——重复 TaskId 直接回缓存（不重执行、不重发事实）
        if (_results.TryGetValue(envelope.TaskId, out var cached))
        {
            context.Respond(new DomainResponse(cached));
            return;
        }

        LogRequestStarted(_instanceContext.Logger, InstanceName, envelope.TaskId,
            envelope.Capability ?? "unknown", envelope.Operation ?? "unknown");

        var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();

        // DC-14：已取消请求 fail-fast——不执行 handler（06 §1 取消贯穿；记录失败非异常升级）
        TaskResultEnvelope result;
        if (request.CancellationToken.IsCancellationRequested)
        {
            result = CancelledResult(envelope);
        }
        else
        {
            // P68 归因分离：终端 handler 崩溃 = 业务监督面（05 §2 重启契约）——
            // 先回填 future（调用方立即得失败结果，不挂死）再上抛触发 OneForOne 重启
            try
            {
                result = await ExecuteRequestAsync(envelope, request.CancellationToken).ConfigureAwait(false);
            }
            catch (HandlerFaultException fault)
            {
                await RespondHandlerFaultAsync(context, envelope, fault).ConfigureAwait(false);
                RecordObservations(envelope, startedAt, succeeded: false);
                throw; // actor 崩溃 → 监督重启（05 §2；context.Respond 已送达——future 已完成）
            }
        }

        RecordResult(envelope.TaskId, result);
        await PublishFactAsync(envelope, result).ConfigureAwait(false); // DC-11：任务完成/失败事实
        RecordObservations(envelope, startedAt, result.Succeeded); // 先观测后 Respond——future 完成即观测已落地（无竞态窗口）
        context.Respond(new DomainResponse(result));
    }

    /// <summary>完成面观测（P70-T3）：出边界日志 + requests/duration 计量 + 慢请求告警。</summary>
    private void RecordObservations(TaskEnvelope envelope, long startedAt, bool succeeded)
    {
        var durationMs = System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        var capability = envelope.Capability ?? "unknown";
        LogRequestCompleted(_instanceContext.Logger, InstanceName, envelope.TaskId, succeeded, durationMs);
        Keystone.Runtime.Trace.KeystoneMeter.ActorRequests.Add(
            1, [new("capability", capability), new("instance", InstanceName)]);
        Keystone.Runtime.Trace.KeystoneMeter.ActorRequestDuration.Record(
            durationMs, [new("capability", capability)]);
        if (durationMs > _slowRequestThreshold.TotalMilliseconds)
        {
            Keystone.Runtime.Trace.KeystoneMeter.SlowRequests.Add(1, [new("capability", capability)]);
            LogSlowRequest(_instanceContext.Logger, InstanceName, envelope.TaskId, durationMs,
                _slowRequestThreshold.TotalMilliseconds);
        }
    }

    /// <summary>DC-14：执行单请求——请求 CT 经实例 context 暴露；取消/异常均转失败结果
    ///（异常必须回填 future——否则 Proto.Future 永不完成，无超时调用方永久挂起，P68）。</summary>
    private async Task<TaskResultEnvelope> ExecuteRequestAsync(TaskEnvelope envelope, CancellationToken requestCt)
    {
        _instanceContext.SetRequestCancellationToken(requestCt);
        try
        {
            return await ExecuteTracedAsync(envelope).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 中间件/handler 主动取消 → 任务失败（不升级监督重启）
            return CancelledResult(envelope);
        }
        catch (HandlerFaultException)
        {
            throw; // 终端 handler 崩溃——上抛 ReceiveAsync 走"回填 + 监督重启"路径（P68 归因）
        }
        // CA1031：管道/中间件可抛任意异常（D-6 后含服务重复注册等业务错）——
        // 一律转任务失败回填（否则 Proto.Future 永不完成，无超时调用方永久挂起）
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // 消息模型调试性（P68）：actor 边界是异常最后可见点——必须落日志
            //（category = 实例名；TaskId 关联事实事件与 trace）
            Keystone.Runtime.Trace.KeystoneMeter.ActorFaults.Add(
                1, [new("instance", InstanceName), new("faultType", "pipeline")]);
            LogPipelineFault(_instanceContext.Logger, InstanceName, envelope.TaskId, ex.GetType().Name, ex.Message, ex);
            return FailedResult(envelope, ex);
        }
        finally
        {
            _instanceContext.SetRequestCancellationToken(CancellationToken.None);
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
            _currentResult = InvokeHandlerMarked(_handler, _currentEnvelope!);
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
            var result = await ExecuteAsync(envelope).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                // 管道短路/失败结果（未上抛形态）→ 失败 span 标 Error
                activity.SetStatus(
                    System.Diagnostics.ActivityStatusCode.Error, result.ErrorDetail ?? result.ErrorCode);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            throw; // 取消不标错（正常取消语义，非故障）
        }
        catch (Exception ex)
        {
            // handler 崩溃（HandlerFaultException）/中间件异常 → 失败 span 标 Error
            activity.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            activity.Dispose();
        }
    }

    /// <summary>DC-14：取消结果（PipelineCancelled——06 §1 取消贯穿；失败不升级监督）。</summary>
    // CA1848：编译期委托（结构化字段：instance/taskId/type/message）
    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Error,
        Message = "actor '{instance}' task {taskId} pipeline fault: {faultType}: {faultMessage}")]
    private static partial void LogPipelineFault(
        ILogger logger, string instance, Guid taskId, string faultType, string faultMessage, Exception exception);

    // P70-T3：消息边界常规记录（进 Debug / 出 Information 含耗时；慢请求 Warning）
    [LoggerMessage(EventId = 6002, Level = LogLevel.Debug,
        Message = "actor '{instance}' task {taskId} start: {capability}/{operation}")]
    private static partial void LogRequestStarted(
        ILogger logger, string instance, Guid taskId, string capability, string operation);

    [LoggerMessage(EventId = 6003, Level = LogLevel.Information,
        Message = "actor '{instance}' task {taskId} completed: succeeded={succeeded} durationMs={durationMs:F1}")]
    private static partial void LogRequestCompleted(
        ILogger logger, string instance, Guid taskId, bool succeeded, double durationMs);

    [LoggerMessage(EventId = 6004, Level = LogLevel.Warning,
        Message = "slow request: actor '{instance}' task {taskId} took {durationMs:F1}ms (threshold {thresholdMs:F0}ms)")]
    private static partial void LogSlowRequest(
        ILogger logger, string instance, Guid taskId, double durationMs, double thresholdMs);

    /// <summary>P68 归因：终端 handler 调用统一包裹——崩溃打 <see cref="HandlerFaultException"/> 标记
    ///（区别于中间件异常：监督契约保留——回填后仍上抛触发 OneForOne 重启，05 §2）。</summary>
    private static TaskResultEnvelope InvokeHandlerMarked(
        Func<TaskEnvelope, Task<TaskResultEnvelope>> handler, TaskEnvelope envelope)
    {
        try
        {
            return handler(envelope).GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new HandlerFaultException(ex);
        }
    }

    /// <summary>handler 崩溃路径：日志 + 结果缓存 + 事实 + 回填（Respond 后由调用方上抛触发重启）。</summary>
    private async Task RespondHandlerFaultAsync(IContext context, TaskEnvelope envelope, HandlerFaultException fault)
    {
        var inner = fault.InnerException;
        Keystone.Runtime.Trace.KeystoneMeter.ActorFaults.Add(
            1, [new("instance", InstanceName), new("faultType", "handler")]);
        LogPipelineFault(
            _instanceContext.Logger, InstanceName, envelope.TaskId,
            inner.GetType().Name, inner.Message, inner);
        var result = FailedResult(envelope, inner);
        RecordResult(envelope.TaskId, result);
        await PublishFactAsync(envelope, result).ConfigureAwait(false);
        context.Respond(new DomainResponse(result));
    }

    /// <summary>终端 handler 崩溃标记（P68）：内层为业务原始异常。
    /// CA1032：标准构造器无意义（标记类型只经单参工厂创建）——豁免。</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "标记类型仅经单参构造创建（inner 必填）")]
    private sealed class HandlerFaultException(Exception inner) : Exception("terminal handler faulted", inner)
    {
        /// <summary>业务原始异常（构造保证非空——CS8602 消解）。</summary>
        public new Exception InnerException => base.InnerException!;
    }

    /// <summary>P68（19 号审计回归发现）：管道异常 → 失败结果回填——future 不悬挂。
    /// 05 §4 语义：handler/中间件异常 = 任务失败（错误面带异常消息），不升级监督重启。</summary>
    private static TaskResultEnvelope FailedResult(TaskEnvelope envelope, Exception ex) => new()
    {
        TaskId = envelope.TaskId,
        Succeeded = false,
        Type = TaskResultType.Failed,
        ErrorCode = Keystone.Core.Errors.ErrorCode.PipelineExecutionFailed,
        ErrorDetail = ex.Message,
    };

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
        if (_results.Count >= _resultCacheCapacity && _results.ContainsKey(taskId) is false)
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
        public Task<TaskResultEnvelope> ExecuteAsync(TaskEnvelope envelope)
            => Task.FromResult(InvokeHandlerMarked(handler, envelope));

        Task IPipeline.InvokeAsync(IPluginContext context) => throw new NotSupportedException(
            "direct pipeline executes via ExecuteAsync (handler terminal inlined)");
    }
}
