using Keystone.Runtime.Context;
using Keystone.Runtime.Events;

namespace Keystone.Runtime.Tests;

/// <summary>
/// P67 D-8（19 号审计 EV-1，对齐 events.ts:159-176 dispatch）：缺省订阅 = 广播
/// （跨 scope 可收——Cordis ctx.on 无 filter，hook.global || !filter 即投递）；
/// filter 仅显式声明（ScopeFilter = true = 祖先链过滤，等价 Cordis internal/service 的
/// isolate filter 显式携带）。修复前缺省祖先链过滤（G15 语义收窄）。
/// </summary>
public class EventBroadcastDefaultTests
{
    private sealed record Ping(string Value);

    [Fact]
    public async Task Default_subscription_receives_sibling_event()
    {
        var root = new ContextFacade("root", null);
        var left = new ContextFacade("left", root);
        var right = new ContextFacade("right", root);
        var received = 0;
        left.Events.Subscribe<Ping>(_ => received++); // 无选项 = 广播

        await right.Events.EmitAsync(new Ping("x"), right);

        Assert.Equal(1, received); // 兄弟可收（修复前祖先链过滤 → 0）
    }

    [Fact]
    public async Task Explicit_scope_filter_opt_in_ancestor_chain()
    {
        var root = new ContextFacade("root", null);
        var left = new ContextFacade("left", root);
        var right = new ContextFacade("right", root);
        var received = 0;
        left.Events.Subscribe<Ping>(_ => received++, new EventSubscriptionOptions
        {
            Scope = left,
            ScopeFilter = true, // 显式声明过滤（旧 G15 语义 opt-in）
        });

        await right.Events.EmitAsync(new Ping("x"), right);

        Assert.Equal(0, received); // 兄弟非祖先 → 不投递

        await left.Events.EmitAsync(new Ping("y"), left);
        Assert.Equal(1, received); // 自身发布仍投递
    }

    [Fact]
    public async Task Ancestor_delivery_unchanged_under_broadcast()
    {
        var parent = new ContextFacade("parent", null);
        var child = new ContextFacade("child", parent);
        var received = 0;
        parent.Events.Subscribe<Ping>(_ => received++);

        await child.Events.EmitAsync(new Ping("x"), child);

        Assert.Equal(1, received); // 祖先收子分发（广播语义下不变）
    }
}
