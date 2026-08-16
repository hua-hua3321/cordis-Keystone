using Keystone.Runtime.Context;
using Keystone.Runtime.Plugins.Lifecycle;
using Keystone.Runtime.Plugins.Manifest;
using Keystone.Runtime.Plugins.Services;

namespace Keystone.Runtime.Tests;

/// <summary>
/// G-C2 依赖恢复测试（16-cordis-gap-review）：依赖消失 → 依赖方卸载（DISPOSED）；
/// 依赖重现 → 依赖方**自动重启**（对齐 Cordis epoch 驱动，fiber.ts:625-639）。
/// </summary>
public class DependencyReArmTests
{
    private sealed class FakePlugin : IPlugin
    {
        public int InitializeCount;
        public int DisposeCount;

        public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
        {
            Interlocked.Increment(ref InitializeCount);
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            Interlocked.Increment(ref DisposeCount);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Dependency_reappearance_restarts_dependent_plugin()
    {
        var store = new KeyedServiceStore();
        var discovery = new InMemoryServiceDiscovery(store);
        store.Provide("fs", string.Empty, new object(), "provider-x");
        var plugin = new FakePlugin();
        var runtime = new PluginRuntime(
            new PluginManifest("p", "1.0.0", "P.cs", ["cordis-runtime"], [], ["fs"]),
            _ => plugin,
            discovery,
            _ => new ContextFacade("p"));

        await runtime.StartAsync();
        Assert.Equal(PluginLifecycleState.Active, runtime.State);
        Assert.Equal(1, plugin.InitializeCount);

        // 依赖消失 → 依赖方卸载（DISPOSED）
        store.Remove("fs", string.Empty, "provider-x");
        await WaitForStateAsync(runtime, PluginLifecycleState.Pending); // P2-13：可 re-arm 存活态（原 Disposed）
        Assert.Equal(PluginLifecycleState.Pending, runtime.State);
        Assert.Equal(1, plugin.DisposeCount);

        // 依赖重现 → 依赖方自动重启（G-C2 re-arm）
        store.Provide("fs", string.Empty, new object(), "provider-x");
        await WaitForStateAsync(runtime, PluginLifecycleState.Active);
        Assert.Equal(PluginLifecycleState.Active, runtime.State);
        Assert.Equal(2, plugin.InitializeCount); // 重启：第二次初始化
        Assert.Equal(1, plugin.DisposeCount);    // 卸载只一次

        await runtime.DisposeAsync();
    }

    private static async Task WaitForStateAsync(PluginRuntime runtime, PluginLifecycleState expected)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (runtime.State != expected)
        {
            cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(10);
        }
    }
}
