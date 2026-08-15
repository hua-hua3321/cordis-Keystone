namespace Keystone.Runtime.Events;

/// <summary>
/// waterfall 监听处理器形状（包裹 next 链）：不调 <paramref name="next"/> 即否决（ADR-0006）。
/// 与管道中间件形状（04 §2 形状 A）同构。
/// </summary>
public delegate Task WaterfallHandler<TEvent>(TEvent e, Func<Task> next, CancellationToken cancellationToken);
