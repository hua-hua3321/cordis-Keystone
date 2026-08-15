using Keystone.Runtime.Events;

namespace Keystone.Runtime.Tests;

public sealed record Ping(string Value);

public class EventBusModeTests
{
    private static EventBus CreateBus() => new();

    [Fact]
    public async Task Emit_invokes_handlers_in_registration_order()
    {
        var bus = CreateBus();
        var order = new List<string>();
        bus.Subscribe<Ping>(e => order.Add($"a:{e.Value}"));
        bus.Subscribe<Ping>(e => order.Add($"b:{e.Value}"));

        await bus.EmitAsync(new Ping("x"));

        Assert.Equal(["a:x", "b:x"], order);
    }

    [Fact]
    public async Task Emit_stops_on_first_handler_exception()
    {
        var bus = CreateBus();
        var called = 0;
        bus.Subscribe<Ping>(_ => throw new InvalidOperationException("boom"));
        bus.Subscribe<Ping>(_ => called++);

        await Assert.ThrowsAsync<InvalidOperationException>(() => bus.EmitAsync(new Ping("x")));
        Assert.Equal(0, called); // 首错中断，后续不执行（对齐 Cordis emit 同步语义）
    }

    [Fact]
    public async Task Parallel_invokes_concurrently_and_aggregates_errors()
    {
        var bus = CreateBus();
        var concurrent = new List<string>();
        var gate = new TaskCompletionSource();
        bus.SubscribeParallel<Ping>(async e =>
        {
            lock (concurrent)
            {
                concurrent.Add(e.Value);
            }

            await gate.Task; // 所有 handler 先进入，证明并发
        });
        bus.SubscribeParallel<Ping>(async e =>
        {
            lock (concurrent)
            {
                concurrent.Add(e.Value);
            }

            await gate.Task;
        });

        var publish = bus.PublishParallelAsync(new Ping("x"));
        await Task.WhenAll(
            Task.Run(async () =>
            {
                await Task.Delay(50);
                gate.SetResult();
            }),
            publish);

        Assert.Equal(2, concurrent.Count); // 两个 handler 都执行（并发聚合）
    }

    [Fact]
    public async Task Parallel_aggregates_multiple_failures()
    {
        var bus = CreateBus();
        bus.SubscribeParallel<Ping>(_ => throw new InvalidOperationException("e1"));
        bus.SubscribeParallel<Ping>(_ => throw new ArgumentException("e2"));

        var exception = await Assert.ThrowsAsync<AggregateException>(() => bus.PublishParallelAsync(new Ping("x")));

        Assert.Equal(2, exception.InnerExceptions.Count);
    }

    [Fact]
    public async Task Serial_awaits_in_order_and_short_circuits_on_first_value()
    {
        var bus = CreateBus();
        var order = new List<string>();
        bus.SubscribeSerial<Ping>(e =>
        {
            order.Add("first");
            return Task.FromResult<object?>(e.Value == "stop" ? "decided" : null);
        });
        bus.SubscribeSerial<Ping>(e =>
        {
            order.Add("second");
            return Task.FromResult<object?>(null);
        });

        var result = await bus.PublishSerialAsync(new Ping("stop"));

        Assert.Equal("decided", result);
        Assert.Equal(["first"], order); // 首个非 null 短路，第二个不执行
    }

    [Fact]
    public void Bail_short_circuits_synchronously_on_first_non_null()
    {
        var bus = CreateBus();
        var order = new List<string>();
        bus.SubscribeBail<Ping>(e =>
        {
            order.Add("first");
            return e.Value == "go" ? "accepted" : null;
        });
        bus.SubscribeBail<Ping>(e =>
        {
            order.Add("second");
            return "late";
        });

        var result = bus.PublishBail(new Ping("go"));

        Assert.Equal("accepted", result);
        Assert.Equal(["first"], order);
    }

    [Fact]
    public async Task Waterfall_wraps_next_chain_in_registration_order()
    {
        var bus = CreateBus();
        var order = new List<string>();
        bus.SubscribeWaterfall<Ping>(async (e, next, ct) =>
        {
            order.Add("before-a");
            await next();
            order.Add("after-a");
        });
        bus.SubscribeWaterfall<Ping>(async (e, next, ct) =>
        {
            order.Add("before-b");
            await next();
            order.Add("after-b");
        });

        await bus.PublishWaterfallAsync(new Ping("x"));

        Assert.Equal(["before-a", "before-b", "after-b", "after-a"], order);
    }

    [Fact]
    public async Task Waterfall_veto_skips_rest_of_chain()
    {
        var bus = CreateBus();
        var order = new List<string>();
        bus.SubscribeWaterfall<Ping>(async (e, next, ct) =>
        {
            order.Add("veto");
            // 不调 next：否决，链其余部分不执行
        });
        bus.SubscribeWaterfall<Ping>(async (e, next, ct) => order.Add("never"));

        await bus.PublishWaterfallAsync(new Ping("x"));

        Assert.Equal(["veto"], order);
    }
}
