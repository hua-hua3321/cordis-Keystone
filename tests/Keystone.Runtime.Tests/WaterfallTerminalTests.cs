using Keystone.Runtime.Events;

namespace Keystone.Runtime.Tests;

/// <summary>
/// G-C6 waterfall 发布者注入 terminal 测试（16-cordis-gap-review）：
/// 发布者可注入内置行为（最内层 next），监听器不调 next 即否决（terminal 未执行），
/// 返回值 = terminal 执行结果（Cordis waterfall 返回值语义，events.ts:234-243）。
/// </summary>
public class WaterfallTerminalTests
{
    private sealed record Event(string Name);

    [Fact]
    public async Task Terminal_result_is_returned_through_chain()
    {
        var bus = new EventBus();
        var order = new List<string>();

        bus.SubscribeWaterfall<Event>(async (e, next, ct) =>
        {
            order.Add("a:before");
            await next();
            order.Add("a:after");
        });
        bus.SubscribeWaterfall<Event>(async (e, next, ct) =>
        {
            order.Add("b:before");
            await next();
            order.Add("b:after");
        });

        // 发布者注入 terminal（内置行为）：执行并返回值
        var result = await bus.PublishWaterfallAsync(new Event("x"), terminal: () =>
        {
            order.Add("terminal");
            return Task.FromResult<object?>("builtin-done");
        });

        Assert.Equal("builtin-done", result);
        Assert.Equal(["a:before", "b:before", "terminal", "b:after", "a:after"], order);
    }

    [Fact]
    public async Task Listener_veto_skips_terminal()
    {
        var bus = new EventBus();
        var terminalCalled = false;

        bus.SubscribeWaterfall<Event>(async (e, next, ct) =>
        {
            // 不调 next → 否决（terminal 不执行）
            await Task.CompletedTask;
        });

        var result = await bus.PublishWaterfallAsync(new Event("x"), terminal: () =>
        {
            terminalCalled = true;
            return Task.FromResult<object?>("builtin");
        });

        Assert.False(terminalCalled); // 否决：terminal 未执行
        Assert.Null(result);
    }

    [Fact]
    public async Task Without_terminal_returns_null()
    {
        var bus = new EventBus();

        bus.SubscribeWaterfall<Event>(async (e, next, ct) => await next());

        var result = await bus.PublishWaterfallAsync(new Event("x"));

        Assert.Null(result); // 无 terminal → 空操作结果 null
    }
}
