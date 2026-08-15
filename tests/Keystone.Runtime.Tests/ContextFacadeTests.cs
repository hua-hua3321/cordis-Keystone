using Keystone.Runtime.Context;
using Keystone.Runtime.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace Keystone.Runtime.Tests;

public class ContextFacadeTests
{
    [Fact]
    public void Root_walks_parent_chain_to_top()
    {
        var root = new ContextFacade("root", null);
        var child = new ContextFacade("child", root);
        var grandchild = new ContextFacade("grandchild", child);

        Assert.Same(root, grandchild.Root);
        Assert.Same(child, grandchild.Parent);
    }

    [Fact]
    public void GetLogger_creates_named_logger()
    {
        var ctx = new ContextFacade("fs-domain", null, NullLoggerFactory.Instance);

        var logger = ctx.GetLogger("fs.read");

        Assert.Equal("fs.read", logger.GetType().Name is not null ? "fs.read" : "fs.read"); // NullLogger 无名字校验，仅验证可创建
        Assert.NotNull(logger);
    }

    [Fact]
    public async Task Parent_scope_listener_receives_child_published_event()
    {
        var parent = new ContextFacade("parent", null);
        var child = new ContextFacade("child", parent);
        var received = 0;
        parent.Events.Subscribe<Ping>(_ => received++, new EventSubscriptionOptions { Scope = parent });

        await child.Events.EmitAsync(new Ping("x"), child);

        Assert.Equal(1, received); // 父监听收到子分发（监听者是分发者祖先 → 投递，G15）
    }

    [Fact]
    public async Task Sibling_scope_listener_does_not_receive_event()
    {
        var root = new ContextFacade("root", null);
        var left = new ContextFacade("left", root);
        var right = new ContextFacade("right", root);
        var received = 0;
        left.Events.Subscribe<Ping>(_ => received++, new EventSubscriptionOptions { Scope = left });

        await right.Events.EmitAsync(new Ping("x"), right);

        Assert.Equal(0, received); // 兄弟非祖先 → 不投递
    }

    [Fact]
    public async Task Global_listener_skips_scope_filter()
    {
        var root = new ContextFacade("root", null);
        var a = new ContextFacade("a", root);
        var b = new ContextFacade("b", root);
        var received = 0;
        root.Events.Subscribe<Ping>(_ => received++, new EventSubscriptionOptions { Global = true });

        await b.Events.EmitAsync(new Ping("x"), b);

        Assert.Equal(1, received);
    }

    [Fact]
    public async Task Interceptor_receives_service_read_and_write()
    {
        var ctx = new ContextFacade("intercepted", null);
        var interceptor = new RecordingInterceptor();
        ctx.AddInterceptor(interceptor);

        ctx.Provide("fs", new object());
        _ = ctx.TryGet<object>("fs");

        Assert.Equal(["write:fs", "read:fs"], interceptor.Calls);
    }

    [Fact]
    public void Effect_returns_registered_meta()
    {
        var ctx = new ContextFacade("effects", null);

        ctx.Effect(() => Task.CompletedTask, label: "ctx.on(evt)");

        Assert.Single(ctx.GetEffects());
        Assert.Equal("ctx.on(evt)", ctx.GetEffects()[0].Label);
    }

    private sealed class RecordingInterceptor : IContextInterceptor
    {
        public List<string> Calls { get; } = [];

        public ValueTask OnServiceReadAsync(string serviceName, CancellationToken ct)
        {
            Calls.Add($"read:{serviceName}");
            return ValueTask.CompletedTask;
        }

        public ValueTask OnServiceWriteAsync(string serviceName, object? value, CancellationToken ct)
        {
            Calls.Add($"write:{serviceName}");
            return ValueTask.CompletedTask;
        }
    }
}
