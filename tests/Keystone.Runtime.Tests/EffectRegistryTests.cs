using Keystone.Runtime.Effects;

namespace Keystone.Runtime.Tests;

public class EffectRegistryTests
{
    [Fact]
    public async Task Register_runs_disposer()
    {
        var registry = new EffectRegistry();
        var disposed = false;
        registry.Register(() =>
        {
            disposed = true;
            return Task.CompletedTask;
        }, label: "cleanup");

        await registry.DisposeAllAsync();

        Assert.True(disposed);
    }

    [Fact]
    public void GetEffects_reports_labels_and_callers()
    {
        var registry = new EffectRegistry();
        registry.Register(() => Task.CompletedTask, label: "ctx.on(evt)"); // [CallerMemberName] 自动注入
        registry.Register(() => Task.CompletedTask, label: "timer");

        var effects = registry.GetEffects();

        Assert.Equal(2, effects.Count);
        Assert.Equal("ctx.on(evt)", effects[0].Label);
        Assert.Equal(nameof(GetEffects_reports_labels_and_callers), effects[1].CallerMember);
        Assert.Empty(effects[0].Children);
    }

    [Fact]
    public async Task Nested_effects_form_tree()
    {
        var registry = new EffectRegistry();
        registry.Register(async () =>
        {
            // 外层 effect 回调内注册内层 effect → 挂为外层 children
            registry.Register(() => Task.CompletedTask, label: "inner");
        }, label: "outer");

        await registry.DisposeAllAsync();

        var effects = registry.GetEffects();
        Assert.Single(effects);
        Assert.Equal("outer", effects[0].Label);
        Assert.Single(effects[0].Children);
        Assert.Equal("inner", effects[0].Children[0].Label);
    }

    [Fact]
    public async Task DisposeAll_runs_in_reverse_registration_order()
    {
        var registry = new EffectRegistry();
        var order = new List<string>();
        registry.Register(() =>
        {
            order.Add("a");
            return Task.CompletedTask;
        }, label: "a");
        registry.Register(() =>
        {
            order.Add("b");
            return Task.CompletedTask;
        }, label: "b");

        await registry.DisposeAllAsync();

        Assert.Equal(["b", "a"], order); // 逆序（quiesce 收敛语义，ADR-0005）
    }
}
