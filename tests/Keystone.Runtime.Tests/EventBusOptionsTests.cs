using Keystone.Runtime.Events;

namespace Keystone.Runtime.Tests;

public class EventBusOptionsTests
{
    [Fact]
    public async Task Prepend_option_places_handler_first()
    {
        var bus = new EventBus();
        var order = new List<string>();
        bus.Subscribe<Ping>(_ => order.Add("normal"));
        bus.Subscribe<Ping>(_ => order.Add("prepended"), new EventSubscriptionOptions { Prepend = true });

        await bus.EmitAsync(new Ping("x"));

        Assert.Equal(["prepended", "normal"], order);
    }

    [Fact]
    public async Task Once_option_auto_unsubscribes_after_first_invocation()
    {
        var bus = new EventBus();
        var calls = 0;
        bus.Subscribe<Ping>(_ => calls++, new EventSubscriptionOptions { Once = true });

        await bus.EmitAsync(new Ping("a"));
        await bus.EmitAsync(new Ping("b"));

        Assert.Equal(1, calls);
    }
}
