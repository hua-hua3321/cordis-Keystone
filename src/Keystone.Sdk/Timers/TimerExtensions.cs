using Keystone.Runtime.Context;

namespace Keystone.Sdk.Timers;

/// <summary>
/// 插件计时器（10 §4，对齐 @cordisjs/plugin-timer）：全部经 ctx.Context.Effect 注册
/// ——插件卸载（quiesce）时自动取消/排空，作者无需手动清理（N3）。
/// </summary>
public static class TimerExtensions
{
    /// <summary>延迟执行一次。</summary>
    public static ITimerHandle SetTimeout(this IPluginContext ctx, Func<Task> callback, TimeSpan delay)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(callback);
        return new TimerHandle(ctx, "timeout", callback, delay, repeat: false, throttleWindow: null, debounceWindow: null);
    }

    /// <summary>周期重复执行。</summary>
    public static ITimerHandle SetInterval(this IPluginContext ctx, Func<Task> callback, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(callback);
        return new TimerHandle(ctx, "interval", callback, period, repeat: true, throttleWindow: null, debounceWindow: null);
    }

    /// <summary>节流：窗口内最多执行一次（首次立即）。</summary>
    public static ITimerHandle Throttle(this IPluginContext ctx, Func<Task> callback, TimeSpan window)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(callback);
        return new TimerHandle(ctx, "throttle", callback, window, repeat: false, throttleWindow: window, debounceWindow: null);
    }

    /// <summary>防抖：窗口内合并为一次（最后一次触发后延迟执行）。</summary>
    public static ITimerHandle Debounce(this IPluginContext ctx, Func<Task> callback, TimeSpan window)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(callback);
        return new TimerHandle(ctx, "debounce", callback, window, repeat: false, throttleWindow: null, debounceWindow: window);
    }

    private sealed class TimerHandle : ITimerHandle
    {
        private readonly IPluginContext _ctx;
        private readonly Func<Task> _callback;
        private readonly TimeSpan _delay;
        private readonly bool _repeat;
        private readonly TimeSpan? _throttleWindow;
        private readonly TimeSpan? _debounceWindow;
        private readonly CancellationTokenSource _cts = new();
        private readonly Lock _lock = new();
        private DateTimeOffset _nextAllowed;
        private Timer? _debounceTimer;
        private bool _disposed;

        public TimerHandle(
            IPluginContext ctx,
            string label,
            Func<Task> callback,
            TimeSpan delay,
            bool repeat,
            TimeSpan? throttleWindow,
            TimeSpan? debounceWindow)
        {
            _ctx = ctx;
            _callback = callback;
            _delay = delay;
            _repeat = repeat;
            _throttleWindow = throttleWindow;
            _debounceWindow = debounceWindow;
            _nextAllowed = DateTimeOffset.UtcNow;

            if (repeat || (throttleWindow is null && debounceWindow is null)) // timeout/interval 启动循环；throttle/debounce 由 Trigger 驱动
            {
                _ = RunLoopAsync(_cts.Token); // timeout/interval 启动循环；throttle/debounce 由 Trigger 驱动
            }

            // 随插件卸载回收（quiesce → Effect 逆序收敛 → 取消）
            ctx.Context.Effect(() =>
            {
                _cts.Cancel();
                return Task.CompletedTask;
            }, label: $"timer:{label}");
        }

        public void Trigger()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                if (_throttleWindow is { } window)
                {
                    var now = DateTimeOffset.UtcNow;
                    if (now < _nextAllowed)
                    {
                        return; // 窗口内跳过
                    }

                    _nextAllowed = now + window;
                    _ = FireSafeAsync();
                    return;
                }

                if (_debounceWindow is { } debounce)
                {
                    // 单 Timer 实例 + Change 重置（避免重建竞态）
                    _debounceTimer ??= new Timer(
                        _ =>
                        {
                            lock (_lock)
                            {
                                _debounceTimer?.Dispose();
                                _debounceTimer = null;
                            }

                            _ = FireSafeAsync();
                        },
                        null,
                        Timeout.InfiniteTimeSpan,
                        Timeout.InfiniteTimeSpan);
                    _debounceTimer.Change(debounce, Timeout.InfiniteTimeSpan);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _debounceTimer?.Dispose();
                _debounceTimer = null;
                _cts.Cancel();
            }

            _cts.Dispose();
            await Task.CompletedTask.ConfigureAwait(false);
        }

        private async Task RunLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(_delay, ct).ConfigureAwait(false);
                    await FireSafeAsync().ConfigureAwait(false);
                    if (!_repeat)
                    {
                        return; // timeout 单次
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 取消 = 正常退出（quiesce）
            }
        }

        private async Task FireSafeAsync()
        {
            try
            {
                await _callback().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // 计时器回调异常不崩溃宿主（CA1031：回调可抛任意异常，记日志由宿主侧兜底）
            }
        }
    }
}
