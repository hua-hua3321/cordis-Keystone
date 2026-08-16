using Keystone.Runtime.Context;
using Keystone.Sdk.Timers;

namespace Keystone.Sdk.Tests;

/// <summary>
/// P64（P0-7，19 号审计 CF-3）：CA-9 加固收口——throttle/debounce 路径的 quiesce 语义。
/// 修复前：① debounce 已武装原生 Timer 不取消 → 卸载后到点仍执行插件回调；
/// ② throttle 在途回调无人等；③ effect disposer 不置 _disposed → quiesce 后 Trigger 仍可开火。
/// </summary>
public class TimerQuiesceHardeningTests
{
    [Fact]
    public async Task Debounce_quiesce_does_not_fire_after_unload()
    {
        // 独立干净用例：Trigger 武装 → quiesce → 窗口过后不触发
        var ctx = new ContextFacade("test");
        var fired = false;
        var handle = ctx.Debounce(() =>
        {
            fired = true;
            return Task.CompletedTask;
        }, TimeSpan.FromMilliseconds(60));

        handle.Trigger(); // 武装
        await ctx.DisposeEffectsAsync(); // 立即 quiesce
        await Task.Delay(180);

        Assert.False(fired);
    }

    [Fact]
    public async Task Quiesce_awaits_inflight_throttle_callback()
    {
        var ctx = new ContextFacade("test");
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handle = ctx.Throttle(async () =>
        {
            await Task.Delay(150); // 慢回调在途
            done.TrySetResult();
        }, TimeSpan.FromMilliseconds(1));

        handle.Trigger();
        await Task.Delay(20); // 等回调进入在途
        await ctx.DisposeEffectsAsync(); // quiesce 应等在途回调

        Assert.True(done.Task.IsCompleted, "quiesce 返回时在途 throttle 回调应已完成");
    }

    [Fact]
    public async Task Quiesce_prevents_subsequent_trigger_firing()
    {
        var ctx = new ContextFacade("test");
        var fired = 0;
        var handle = ctx.Throttle(() =>
        {
            Interlocked.Increment(ref fired);
            return Task.CompletedTask;
        }, TimeSpan.FromMilliseconds(1));

        await ctx.DisposeEffectsAsync(); // quiesce → 已卸载
        handle.Trigger(); // quiesce 后触发
        await Task.Delay(100);

        Assert.Equal(0, fired); // 不得开火（_disposed 应在 effect 收敛时置位）
    }
}
