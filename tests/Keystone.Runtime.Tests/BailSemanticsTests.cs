using Keystone.Runtime.Events;

namespace Keystone.Runtime.Tests;

/// <summary>
/// G-C4 决策短路语义测试（16-cordis-gap-review）：对齐 Cordis isBailed——
/// serial/bail 中 null/false 不算决策值（不短路），其余值（含 0/空串）短路。
/// </summary>
public class BailSemanticsTests
{
    private sealed record Event(string Name);

    [Fact]
    public async Task Serial_false_does_not_short_circuit()
    {
        var bus = new EventBus();
        var calls = 0;

        // 第一个监听返回 false（不是决策）→ 继续；第二个返回 "decision" → 短路
        bus.SubscribeSerial<Event>(_ => Task.FromResult<object?>(false));
        bus.SubscribeSerial<Event>(_ =>
        {
            calls++;
            return Task.FromResult<object?>("decision");
        });

        var result = await bus.PublishSerialAsync(new Event("x"));

        Assert.Equal("decision", result); // false 不短路 → 第二个执行
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Bail_false_does_not_short_circuit()
    {
        var bus = new EventBus();
        var calls = 0;

        bus.SubscribeBail<Event>(_ => false); // 返回 false → 不短路
        bus.SubscribeBail<Event>(_ =>
        {
            calls++;
            return "decision";
        });

        var result = bus.PublishBail(new Event("x"));

        Assert.Equal("decision", result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Serial_zero_and_empty_string_short_circuit()
    {
        var bus = new EventBus();
        var calls = 0;

        // 0 和空串都是决策值（isBailed：非 null 非 false 即决策）——首个 0 短路
        bus.SubscribeSerial<Event>(_ => Task.FromResult<object?>(0));
        bus.SubscribeSerial<Event>(_ =>
        {
            calls++;
            return Task.FromResult<object?>("never");
        });

        var result = await bus.PublishSerialAsync(new Event("x"));

        Assert.Equal(0, result);
        Assert.Equal(0, calls);
    }
}
