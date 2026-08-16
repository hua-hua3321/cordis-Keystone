using Keystone.Runtime.Context;
using Keystone.Runtime.Plugins.Lifecycle;
using Keystone.Runtime.Plugins.Manifest;
using Keystone.Runtime.Plugins.Services;

namespace Keystone.Runtime.Tests;

/// <summary>
/// DC-5 测试（17-doc-compliance-audit，ADR-0007 风险表）：
/// 依赖永不就绪 → 启动超时 → FAILED（不无限 PENDING 挂起）。
/// </summary>
public class DependencyTimeoutTests
{
    private sealed class FakePlugin : IPlugin
    {
        public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
            => Task.CompletedTask;

        public Task DisposeAsync() => Task.CompletedTask;
    }

    [Fact]
    public async Task Missing_dependency_times_out_to_failed()
    {
        var discovery = new InMemoryServiceDiscovery(new KeyedServiceStore());
        var runtime = new PluginRuntime(
            new PluginManifest("p", "1.0.0", "P.cs", ["cordis-runtime"], [], ["never-available"]),
            _ => new FakePlugin(),
            discovery,
            _ => new ContextFacade("p"),
            dependencyTimeout: TimeSpan.FromMilliseconds(200)); // 短超时

        var start = runtime.StartAsync();
        Assert.Equal(PluginLifecycleState.Pending, runtime.State); // 依赖缺失 → PENDING

        await start; // 200ms 后超时 → FAILED

        Assert.Equal(PluginLifecycleState.Failed, runtime.State); // 不无限挂起
        await Assert.ThrowsAsync<Keystone.Core.Errors.KeystoneException>(() => runtime.AwaitAsync()); // 错误可查

        await runtime.DisposeAsync();
    }

    [Fact]
    public async Task Dependency_appearing_before_timeout_still_activates()
    {
        var store = new KeyedServiceStore();
        var runtime = new PluginRuntime(
            new PluginManifest("p", "1.0.0", "P.cs", ["cordis-runtime"], [], ["late-dep"]),
            _ => new FakePlugin(),
            new InMemoryServiceDiscovery(store),
            _ => new ContextFacade("p"),
            dependencyTimeout: TimeSpan.FromSeconds(5));

        var start = runtime.StartAsync();
        Assert.Equal(PluginLifecycleState.Pending, runtime.State);

        await Task.Delay(50);
        store.Provide("late-dep", string.Empty, new object(), "provider"); // 超时前值出现（即注册）

        await start;
        Assert.Equal(PluginLifecycleState.Active, runtime.State); // 正常激活

        await runtime.DisposeAsync();
    }
}
