namespace Keystone.Runtime.Persistence;

/// <summary>
/// 事实保留调度器（DC-18，ADR-0009 决策 3 风险表"Prune 定时执行"）：
/// PeriodicTimer 周期调用 <see cref="IEventStore.PruneAsync"/>。
/// Prune 失败**降级吞掉**（记续跑）——事件写入是旁路（03 §4 事件分层），
/// 保留策略失败不得拖垮宿主主链（ADR-0009"不阻塞主链路是硬约束"）。
/// </summary>
public sealed class FactRetentionScheduler : IDisposable
{
    private readonly IEventStore _store;
    private readonly RetentionPolicy _policy;
    private readonly TimeSpan _interval;
    private readonly Lock _lock = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public FactRetentionScheduler(IEventStore store, RetentionPolicy policy, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(policy);
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "prune interval must be positive");
        }

        _store = store;
        _policy = policy;
        _interval = interval;
    }

    /// <summary>启动周期 Prune（幂等——重复 Start 无效果）。</summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_loop is not null)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _loop = Task.Run(() => LoopAsync(token));
        }
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    await _store.PruneAsync(_policy, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw; // 关闭取消——退出循环
                }
#pragma warning disable CA1031 // 存储实现可抛任意异常（IO/序列化），降级续跑是设计语义（旁路硬约束）
                catch (Exception)
                {
                    // 降级：本轮 Prune 失败（IO 占用等）跳过，下轮重试
                }
#pragma warning restore CA1031
            }
        }
        catch (OperationCanceledException)
        {
            // 正常关闭
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _loop = null;
        }

        GC.SuppressFinalize(this);
    }
}
