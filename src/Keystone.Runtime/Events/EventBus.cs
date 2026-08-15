using Keystone.Runtime.Context;
using Keystone.Runtime.Persistence;

namespace Keystone.Runtime.Events;

/// <summary>
/// 事件总线实现：按事件类型 + 分发模式分组注册，五模式聚合分发（ADR-0006），
/// scope 过滤（G15：监听者是发布者祖先/自身才投递；global 跳过）。
/// 总线实例在 context 链间**共享**（子 context 复用父的总线，对齐 Cordis 单事件系统 +
/// 监听 filter）；发布者由发布方法显式携带（ID-08）。
/// DC-11（ADR-0009/03 §4）：注入 <see cref="IEventStore"/> 后，<see cref="IFactEvent"/>
/// 在 emit 分发时持久化（durable 失败传播；非 durable 尽力写降级）。
/// </summary>
public sealed class EventBus : IEventBus
{
    private const int FactSchemaVersion = 1;

    private readonly Lock _lock = new();
    private readonly Dictionary<Type, List<HandlerEntry>> _handlers = [];
    private readonly IEventStore? _eventStore;

    /// <summary>创建总线（可选事实持久化存储，DC-11；null = 不持久化）。</summary>
    public EventBus(IEventStore? eventStore = null)
    {
        _eventStore = eventStore;
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler, EventSubscriptionOptions? options = null)
        => Register(typeof(TEvent), handler, DispatchMode.Emit, options);

    public IDisposable SubscribeParallel<TEvent>(Func<TEvent, Task> handler, EventSubscriptionOptions? options = null)
        => Register(typeof(TEvent), handler, DispatchMode.Parallel, options);

    public IDisposable SubscribeSerial<TEvent>(Func<TEvent, Task<object?>> handler, EventSubscriptionOptions? options = null)
        => Register(typeof(TEvent), handler, DispatchMode.Serial, options);

    public IDisposable SubscribeBail<TEvent>(Func<TEvent, object?> handler, EventSubscriptionOptions? options = null)
        => Register(typeof(TEvent), handler, DispatchMode.Bail, options);

    public IDisposable SubscribeWaterfall<TEvent>(WaterfallHandler<TEvent> handler, EventSubscriptionOptions? options = null)
        => Register(typeof(TEvent), handler, DispatchMode.Waterfall, options);

    public async Task EmitAsync<TEvent>(TEvent e, IContext? publisher = null, CancellationToken cancellationToken = default)
    {
        // DC-11：事实事件先记录后分发（观察者异常不丢事实；ADR-0009 决策 3 降级语义）
        if (e is IFactEvent fact)
        {
            await PersistFactAsync(fact).ConfigureAwait(false);
        }

        foreach (var entry in Snapshot<TEvent>(DispatchMode.Emit))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ShouldDispatch(entry, publisher))
            {
                continue;
            }

            MarkOnce(entry);
            ((Action<TEvent>)entry.Handler)(e); // 同步调用：首错传播（对齐 Cordis emit）
        }
    }

    /// <summary>事实持久化（DC-11，ADR-0009 决策 3）：非 durable 尽力写（失败降级）；durable 写失败传播。</summary>
    private async Task PersistFactAsync(IFactEvent fact)
    {
        if (_eventStore is null)
        {
            return;
        }

        var stored = new StoredFact
        {
            SchemaVersion = FactSchemaVersion,
            FactId = Guid.NewGuid(),
            EventName = fact.GetType().Name,
            TaskId = fact.TaskId,
            Capability = fact.Capability,
            PayloadBytes = fact.Payload,
            Timestamp = DateTimeOffset.UtcNow,
            Durable = fact.Durable, // DC-18：分级随事实落盘（重放/归档方可见"必须存活"标记）
        };

        try
        {
            await _eventStore.AppendAsync(stored).ConfigureAwait(false);
        }
        catch (Exception) when (!fact.Durable)
        {
            // 尽力写降级：持久化失败不影响主链路（记日志/告警由嵌入方经 store 实现承担）
        }
    }

    public async Task PublishParallelAsync<TEvent>(TEvent e, IContext? publisher = null, CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>();
        foreach (var entry in Snapshot<TEvent>(DispatchMode.Parallel))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ShouldDispatch(entry, publisher))
            {
                continue;
            }

            MarkOnce(entry);
            try
            {
                tasks.Add(((Func<TEvent, Task>)entry.Handler)(e));
            }
            catch (Exception ex)
            {
                tasks.Add(Task.FromException(ex)); // 同步抛出的 handler 异常也纳入聚合（CA1031：handler 可抛任意异常，聚合是设计语义）
            }
        }

        // 错误聚合（ADR-0006）：await WhenAll 会解包单异常，这里主动聚合成 AggregateException 保证"聚合"语义
        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (Exception)
        {
            var failures = tasks
                .Where(t => t.IsFaulted)
                .SelectMany(t => t.Exception?.InnerExceptions ?? []);
            throw new AggregateException(failures);
        }
    }

    public async Task<object?> PublishSerialAsync<TEvent>(TEvent e, IContext? publisher = null, CancellationToken cancellationToken = default)
    {
        foreach (var entry in Snapshot<TEvent>(DispatchMode.Serial))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ShouldDispatch(entry, publisher))
            {
                continue;
            }

            MarkOnce(entry);
            var result = await ((Func<TEvent, Task<object?>>)entry.Handler)(e).ConfigureAwait(false);
            if (IsBailed(result))
            {
                return result; // 首个决策值短路（对齐 Cordis isBailed：null/false 不短路）
            }
        }

        return null;
    }

    public object? PublishBail<TEvent>(TEvent e, IContext? publisher = null)
    {
        foreach (var entry in Snapshot<TEvent>(DispatchMode.Bail))
        {
            if (!ShouldDispatch(entry, publisher))
            {
                continue;
            }

            MarkOnce(entry);
            var result = ((Func<TEvent, object?>)entry.Handler)(e);
            if (IsBailed(result))
            {
                return result;
            }
        }

        return null;
    }

    public async Task<object?> PublishWaterfallAsync<TEvent>(
        TEvent e,
        IContext? publisher = null,
        Func<Task<object?>>? terminal = null,
        CancellationToken cancellationToken = default)
    {
        // G-C6：terminal = 发布者注入的内置行为（最内层 next，可被否决）；缺省空操作。
        // 结果经 next 链回传：最外层调用者得到 terminal 执行结果（Cordis waterfall 返回值）。
        Func<Task<object?>> inner = terminal ?? (() => Task.FromResult<object?>(null));

        // 反向包装 next 链：最后一个监听者的 next = inner；与管道组合同构（04 §2 形状 B）
        // 监听器 next 是 Func<Task>（不返回值）——链值经 TCS 捕获回传
        Func<Task<object?>> next = inner;
        var entries = Snapshot<TEvent>(DispatchMode.Waterfall);
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (!ShouldDispatch(entry, publisher))
            {
                continue;
            }

            MarkOnce(entry);
            var captured = entry;
            var capturedNext = next;
            next = async () =>
            {
                var capture = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                var handler = (WaterfallHandler<TEvent>)captured.Handler;
                var listenerTask = handler(e, () => AwaitChain(capturedNext, capture), cancellationToken);
                await listenerTask.ConfigureAwait(false);
                // 监听器不调 next → 否决（capture 未完成，返回 null）；调了 → 回传链值
                return capture.Task.IsCompleted ? await capture.Task.ConfigureAwait(false) : null;
            };
        }

        return await next().ConfigureAwait(false);
    }

    private static async Task AwaitChain(Func<Task<object?>> next, TaskCompletionSource<object?> capture)
    {
        capture.TrySetResult(await next().ConfigureAwait(false));
    }

    private Subscription Register(Type eventType, Delegate handler, DispatchMode mode, EventSubscriptionOptions? options)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var entry = new HandlerEntry(eventType, handler, mode, options ?? new EventSubscriptionOptions());
        lock (_lock)
        {
            if (!_handlers.TryGetValue(eventType, out var list))
            {
                list = [];
                _handlers[eventType] = list;
            }

            if (entry.Options.Prepend)
            {
                list.Insert(0, entry);
            }
            else
            {
                list.Add(entry);
            }
        }

        return new Subscription(entry, Remove);
    }

    private void Remove(HandlerEntry entry)
    {
        lock (_lock)
        {
            if (_handlers.TryGetValue(entry.EventType, out var list))
            {
                list.Remove(entry);
            }
        }
    }

    private void MarkOnce(HandlerEntry entry)
    {
        if (entry.Options.Once)
        {
            Remove(entry);
        }
    }

    /// <summary>
    /// G15 过滤：监听者是发布者（publisher）的祖先/自身才投递；global 跳过。
    /// 纯总线场景（无 context 上下文，publisher/scope 均 null）→ 放行。
    /// </summary>
    /// <summary>
    /// G-C4（对齐 Cordis isBailed，events.ts:13-15）：决策值判定——null/false 不算决策（不短路），
    /// 其余值（含空字符串/0）算决策。
    /// </summary>
    private static bool IsBailed(object? value)
        => value is not null && value is not false;

    private static bool ShouldDispatch(HandlerEntry entry, IContext? publisher)
    {
        if (entry.Options.Global)
        {
            return true;
        }

        var listenerScope = entry.Options.Scope;
        if (publisher is null || listenerScope is null)
        {
            return true; // 无上下文语义可判定 → 放行
        }

        var current = publisher;
        while (current is not null)
        {
            if (ReferenceEquals(current, listenerScope))
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    private List<HandlerEntry> Snapshot<TEvent>(DispatchMode mode)
    {
        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list))
            {
                return [];
            }

            return list.Where(e => e.Mode == mode).ToList();
        }
    }

    private sealed class HandlerEntry
    {
        public HandlerEntry(Type eventType, Delegate handler, DispatchMode mode, EventSubscriptionOptions options)
        {
            EventType = eventType;
            Handler = handler;
            Mode = mode;
            Options = options;
        }

        public Type EventType { get; }

        public Delegate Handler { get; }

        public DispatchMode Mode { get; }

        public EventSubscriptionOptions Options { get; }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly HandlerEntry _entry;
        private readonly Action<HandlerEntry> _remove;
        private bool _disposed;

        public Subscription(HandlerEntry entry, Action<HandlerEntry> remove)
        {
            _entry = entry;
            _remove = remove;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _remove(_entry);
            GC.SuppressFinalize(this);
        }
    }
}
