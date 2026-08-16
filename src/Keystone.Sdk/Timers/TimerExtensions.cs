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
        private Task _runTask = Task.CompletedTask; // fire 追踪链（P0-7：throttle/debounce 在途回调挂此链）
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
                _runTask = RunLoopAsync(_cts.Token);
            }

            // 随插件卸载回收（quiesce → Effect 逆序收敛，CA-9 + P0-7）：
            // DisposeAsync 三件事——①取消 + 等在途回调（含 throttle 在途，见 RunFireTracked）；
            // ②弃置已武装的 debounce Timer（修复前：armed Timer 卸载后到点仍执行插件回调）；
            // ③置 _disposed（修复前：quiesce 后 Trigger 仍可开火）
            ctx.Context.Effect(() => DisposeAsync().AsTask(), label: $"timer:{label}");
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
                    RunFireTracked(); // P0-7：在途回调挂 _runTask（quiesce 可等）
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

                            RunFireTracked(); // P0-7：与 throttle 同等收敛
                        },
                        null,
                        Timeout.InfiniteTimeSpan,
                        Timeout.InfiniteTimeSpan);
                    _debounceTimer.Change(debounce, Timeout.InfiniteTimeSpan);
                }
            }
        }

        /// <summary>P0-7（19 号审计 CF-3）：fire 链入 _runTask 链——quiesce 的 DisposeAsync 等待在途回调。
        /// 持锁：与 DisposeAsync 的读取互斥（防最后一发 fire 漏追踪）。</summary>
        private void RunFireTracked()
        {
            lock (_lock)
            {
                var fire = FireSafeAsync(); // 异常由 FireSafeAsync 自吞（不会让 WhenAll 链 fault）
                _runTask = Task.WhenAll(_runTask, fire);
            }
        }

        public async ValueTask DisposeAsync()
        {
            Task run;
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _debounceTimer?.Dispose(); // P0-7：弃置已武装的 debounce Timer（卸载后不再触发）
                _debounceTimer = null;
                _cts.Cancel();
                run = _runTask; // 持锁读取（与 RunFireTracked 互斥）
            }

            // CA-9：不 Dispose CTS——与 RunLoop 的 Task.Delay(delay, ct) 竞态会漏 ObjectDisposedException
            // 出其 catch(OperationCanceledException)（未观察任务异常）；Cancel 已足够释放等待者，CTS 可终结
            try
            {
                await run.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // 兜底：RunLoop/fire 异常已自身 catch
            }
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
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
            {
                // 取消 = 正常退出（quiesce）；disposed 竞态兜底（CA-9 消除源头后不应再出现）
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
