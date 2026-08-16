using Keystone.Runtime.Context;

namespace Keystone.Runtime.Tests;

/// <summary>
/// P64（P0-6，19 号审计 CF-1/EV-5）：插件事件订阅随生命周期回收（对齐 Cordis
/// events.ts:254-259——监听器即 fiber effect，卸载自动退订）。
/// 修复前：ContextFacade.Subscribe 直连总线不挂 effect → handler 永驻 root 共享总线 → ALC 钉死。
/// D-9（CF-7）：Effect 句柄 Dispose = 执行 disposer（对齐 fiber.ts:427-442）。
/// </summary>
public class SubscriptionLifecycleTests
{
    [Fact]
    public async Task Plugin_subscriptions_are_released_on_context_disposal()
    {
        var root = new ContextFacade("root");
        var facade = new ContextFacade("plugin-a", parent: root); // 子复用 root 总线（共享链）
        var received = 0;
        facade.Subscribe<string>(_ => Interlocked.Increment(ref received)); // 不手动 Dispose（插件常见写法）

        await root.Events.EmitAsync("x");
        Assert.Equal(1, Volatile.Read(ref received)); // 已注册且投递

        await facade.DisposeEffectsAsync(); // quiesce → 订阅应随之退订

        await root.Events.EmitAsync("y");
        Assert.Equal(1, Volatile.Read(ref received)); // 不再投递（handler 不滞留总线）
    }

    [Fact]
    public async Task Effect_handle_dispose_executes_disposer()
    {
        // D-9：对齐 Cordis——Effect 返回句柄 Dispose = 执行清理（修复前仅取消）
        var ctx = new ContextFacade("test");
        var cleaned = false;
        var handle = ctx.Effect(() =>
        {
            cleaned = true;
            return Task.CompletedTask;
        });

        handle.Dispose();

        Assert.True(cleaned, "Effect 句柄 Dispose 应执行 disposer（using 惯例 + Cordis 语义）");
    }

    [Fact]
    public async Task Effect_handle_dispose_is_idempotent()
    {
        var ctx = new ContextFacade("test");
        var count = 0;
        var handle = ctx.Effect(() =>
        {
            Interlocked.Increment(ref count);
            return Task.CompletedTask;
        });

        handle.Dispose();
        handle.Dispose(); // 幂等
        await ctx.DisposeEffectsAsync(); // 已 Dispose 的不再执行

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Cancelled_effect_disposer_not_executed_on_dispose_all()
    {
        // 显式取消（Dispose）后，DisposeEffectsAsync 不再重复执行
        var ctx = new ContextFacade("test");
        var count = 0;
        var handle = ctx.Effect(() =>
        {
            Interlocked.Increment(ref count);
            return Task.CompletedTask;
        });

        handle.Dispose(); // 已执行一次
        await ctx.DisposeEffectsAsync();

        Assert.Equal(1, count); // 恰一次
    }
}
