using Keystone.Runtime.Context;
using Keystone.Runtime.Plugins.Lifecycle;
using Keystone.Runtime.Plugins.Manifest;
using Keystone.Runtime.Plugins.Services;

namespace Keystone.Runtime.Tests;

/// <summary>
/// G-C3 服务值卸载注销测试（16-cordis-gap-review）：插件运行期 Provide 的服务值，
/// 在插件卸载后必须从共享 store 注销——依赖方不再拿陈旧值（对齐 Cordis provide disposer，reflect.ts）。
/// </summary>
public class ServiceValueUnloadTests
{
    private sealed class FakeProvider : IPlugin
    {
        public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
        {
            // 运行期 Provide（未在 manifest Provides 声明）——G-C3 场景
            context.Provide("dynamic", new object());
            return Task.CompletedTask;
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }

    private sealed class FakeConsumer : IPlugin
    {
        public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
            => Task.CompletedTask;

        public Task DisposeAsync() => Task.CompletedTask;
    }

    [Fact]
    public async Task Runtime_provided_value_is_removed_after_plugin_unloads()
    {
        var registry = new ServiceRegistry();
        var provider = new FakeProvider();
        var root = new ContextFacade("root");
        ContextFacade? providerCtx = null;
        var runtime = new PluginRuntime(
            new PluginManifest("provider", "1.0.0", "P.cs", ["cordis-runtime"], [], []),
            _ => provider,
            registry,
            name =>
            {
                providerCtx = new ContextFacade(name, root);
                return providerCtx;
            });

        await runtime.StartAsync();
        Assert.True(root.TryGet<object>("dynamic") is not null, "运行期 Provide 的值应在 root store");

        await runtime.StopAsync(); // 显式卸载

        Assert.True(root.TryGet<object>("dynamic") is null, "卸载后运行期 Provide 的值应注销（不再陈旧）");
        Assert.False(providerCtx?.Services.TryGet<object>("dynamic", string.Empty) is not null, "共享 store 应注销");

        await runtime.DisposeAsync();
    }

    [Fact]
    public async Task Consumer_does_not_see_stale_value_after_provider_unloads()
    {
        var registry = new ServiceRegistry();
        var root = new ContextFacade("root");
        var provider = new FakeProvider();
        var providerRuntime = new PluginRuntime(
            new PluginManifest("provider", "1.0.0", "P.cs", ["cordis-runtime"], [], []),
            _ => provider,
            registry,
            name => new ContextFacade(name, root));
        await providerRuntime.StartAsync();

        // 消费者（同一 root 下）经父链应能解析到 dynamic
        var consumerCtx = new ContextFacade("consumer", root);
        Assert.True(consumerCtx.TryGet<object>("dynamic") is not null);

        // 提供方卸载 → 值注销 → 消费者解析不到（非陈旧值）
        await providerRuntime.StopAsync();
        Assert.True(consumerCtx.TryGet<object>("dynamic") is null, "卸载后消费者不应再拿到陈旧值");

        await providerRuntime.DisposeAsync();
    }
}
