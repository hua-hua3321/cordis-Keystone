using Keystone.Core.Errors;
using Keystone.Runtime.Context;
using Keystone.Runtime.Plugins.Lifecycle;
using Keystone.Runtime.Plugins.Manifest;
using Keystone.Runtime.Plugins.Services;

namespace Keystone.Runtime.Tests;

/// <summary>
/// 发现层投影 + 门控统一（18 §2 CA-1 第 2 步，P57-T4）：
/// 可用 = 值存在（ctx.Provide 即注册——不再有独立 availability 状态，消灭"门控放行但 Get 落空"）；
/// manifest.provides ⊆ init 期实际 Provide 值（声明未兑现 → FAILED 且报错点名服务）；
/// 门控 realm 感知（isolateMap → 键域匹配才满足，对齐 Cordis notify 域过滤）；
/// InMemoryServiceDiscovery = KeyedServiceStore 只读投影（发现层 seam，未来 Redis/Consul adapter 同接口）。
/// </summary>
public class DiscoveryGatingTests
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
            DisposeCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ProvidingPlugin(string serviceName, object? value = null) : IPlugin
    {
        public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
        {
            context.Provide(serviceName, value ?? new object());
            return Task.CompletedTask;
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }

    private static PluginManifest Manifest(
        string id,
        IReadOnlyList<string>? provides = null,
        IReadOnlyList<string>? inject = null)
        => new(id, "1.0.0", "P.cs", ["cordis-runtime"], provides ?? [], inject ?? []);

    [Fact]
    public async Task Provide_value_is_sole_registration_dependent_activates()
    {
        // 统一核心：provider 只做 ctx.Provide（无任何独立注册调用）→ 依赖方门控打开 → ACTIVE
        var root = new ContextFacade("root");
        var discovery = new InMemoryServiceDiscovery(root.Services);

        var provider = new PluginRuntime(
            Manifest("provider", provides: ["fs"]),
            _ => new ProvidingPlugin("fs"),
            discovery,
            _ => new ContextFacade("provider", root));
        var dependent = new PluginRuntime(
            Manifest("dep", inject: ["fs"]),
            _ => new FakePlugin(),
            discovery,
            _ => new ContextFacade("dep", root));

        await provider.StartAsync();
        await dependent.StartAsync();

        Assert.Equal(PluginLifecycleState.Active, provider.State);
        Assert.Equal(PluginLifecycleState.Active, dependent.State);
        await provider.DisposeAsync();
        await dependent.DisposeAsync();
    }

    [Fact]
    public async Task Declared_provides_without_value_fails_naming_service()
    {
        // 声明未兑现 → FAILED（明确信号，不再"可用但 Get 落空"）
        var root = new ContextFacade("root");
        var discovery = new InMemoryServiceDiscovery(root.Services);
        var runtime = new PluginRuntime(
            Manifest("liar", provides: ["fs", "cache"]),
            _ => new FakePlugin(), // 不 Provide 任何值
            discovery,
            _ => new ContextFacade("liar", root));

        await runtime.StartAsync();

        Assert.Equal(PluginLifecycleState.Failed, runtime.State);
        var error = await Assert.ThrowsAsync<KeystoneException>(() => runtime.AwaitAsync());
        Assert.Equal(ErrorCode.LifecycleLoadFailed, error.Code);
        Assert.NotNull(error.InnerException);
        Assert.Contains("fs", error.InnerException.Message, StringComparison.Ordinal);
        Assert.Contains("cache", error.InnerException.Message, StringComparison.Ordinal);
        Assert.Contains("provides", error.InnerException.Message, StringComparison.Ordinal);
        await runtime.DisposeAsync();
    }

    [Fact]
    public async Task Declared_provides_with_values_activates()
    {
        var root = new ContextFacade("root");
        var discovery = new InMemoryServiceDiscovery(root.Services);
        var runtime = new PluginRuntime(
            Manifest("good", provides: ["fs"]),
            _ => new ProvidingPlugin("fs"),
            discovery,
            _ => new ContextFacade("good", root));

        await runtime.StartAsync();

        Assert.Equal(PluginLifecycleState.Active, runtime.State);
        await runtime.DisposeAsync();
    }

    [Fact]
    public async Task Gating_respects_isolate_realm()
    {
        // 依赖方 isolate 声明 fs → 私有域 #g：共享域的值不满足门控；#g 域的值才满足
        var root = new ContextFacade("root");
        var discovery = new InMemoryServiceDiscovery(root.Services);
        var isolateMap = new Dictionary<string, string>(StringComparer.Ordinal) { ["fs"] = "#g" };
        var runtime = new PluginRuntime(
            Manifest("dep", inject: ["fs"]),
            _ => new FakePlugin(),
            discovery,
            _ => new ContextFacade("dep", root, isolateMap: isolateMap),
            isolateMap: isolateMap,
            dependencyTimeout: TimeSpan.FromSeconds(10));

        var start = runtime.StartAsync();
        Assert.Equal(PluginLifecycleState.Pending, runtime.State);

        root.Services.Provide("fs", string.Empty, new object(), "shared-provider"); // 共享域 → 不满足
        await Task.Delay(150);
        Assert.Equal(PluginLifecycleState.Pending, runtime.State);

        root.Services.Provide("fs", "#g", new object(), "group-provider"); // 私有域 → 满足
        await start;
        Assert.Equal(PluginLifecycleState.Active, runtime.State);
        await runtime.DisposeAsync();
    }

    [Fact]
    public async Task Value_removal_unloads_dependent_reappearance_restarts()
    {
        // G-C2 经值生命周期：值删 → 依赖方卸载；值回 → 自动重启
        var root = new ContextFacade("root");
        var discovery = new InMemoryServiceDiscovery(root.Services);
        var plugin = new FakePlugin();
        var runtime = new PluginRuntime(
            Manifest("dep", inject: ["fs"]),
            _ => plugin,
            discovery,
            _ => new ContextFacade("dep", root));

        var providerFacade = new ContextFacade("provider", root);
        providerFacade.Provide("fs", new object());

        await runtime.StartAsync();
        Assert.Equal(PluginLifecycleState.Active, runtime.State);
        Assert.Equal(1, plugin.InitializeCount);

        providerFacade.RemoveOwnedServices(); // 值消失 → 依赖方卸载
        await WaitForStateAsync(runtime, PluginLifecycleState.Disposed);
        Assert.Equal(1, plugin.DisposeCount);

        var providerAgain = new ContextFacade("provider2", root);
        providerAgain.Provide("fs", new object()); // 值重现 → 自动重启
        await WaitForStateAsync(runtime, PluginLifecycleState.Active);
        Assert.Equal(2, plugin.InitializeCount);
        providerAgain.RemoveOwnedServices();
        await runtime.DisposeAsync();
    }

    [Fact]
    public void InMemoryDiscovery_projects_store_read_and_batch_notification()
    {
        var store = new KeyedServiceStore();
        var discovery = new InMemoryServiceDiscovery(store);
        var batches = new List<IReadOnlyList<ServiceKey>>();
        using var subscription = discovery.Subscribe(batches.Add);

        using var scope = store.BeginNotifyScope();
        var d1 = store.Provide("fs", string.Empty, new object(), "p1");
        store.Provide("cache", "#g", new object(), "p1").Dispose(); // scope 内增删并入同批
        scope.Dispose();

        Assert.True(discovery.IsAvailable("fs", string.Empty));
        Assert.False(discovery.IsAvailable("cache", "#g")); // 已删
        Assert.Equal(["fs"], discovery.AvailableServices(string.Empty));
        var batch = Assert.Single(batches);
        Assert.Equal(2, batch.Count); // 增 + 删 都在批内（2 个键：fs、cache#g）
        d1.Dispose();
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
