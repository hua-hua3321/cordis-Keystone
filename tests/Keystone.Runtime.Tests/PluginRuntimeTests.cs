using Keystone.Runtime.Context;
using Keystone.Runtime.Plugins.Lifecycle;
using Keystone.Runtime.Plugins.Manifest;
using Keystone.Runtime.Plugins.Services;

namespace Keystone.Runtime.Tests;

public class PluginRuntimeTests
{
    private static (KeyedServiceStore Store, InMemoryServiceDiscovery Discovery) CreateDiscovery()
    {
        var store = new KeyedServiceStore();
        return (store, new InMemoryServiceDiscovery(store));
    }

    [Fact]
    public async Task Missing_dependency_holds_pending_until_service_appears()
    {
        var (store, discovery) = CreateDiscovery();
        var plugin = new FakePlugin();
        var states = new List<PluginLifecycleState>();
        var runtime = new PluginRuntime(
            new PluginManifest("p", "1.0.0", "P.cs", ["cordis-runtime"], [], ["fs"]),
            _ => plugin,
            discovery,
            _ => new Context.ContextFacade("p"));

        runtime.StateChanged += (_, args) => states.Add(args.State);

        var start = runtime.StartAsync();
        Assert.Equal(PluginLifecycleState.Pending, runtime.State); // 依赖缺失 → PENDING

        store.Provide("fs", string.Empty, new object(), "provider-x"); // 值出现 → 可用（值即注册）→ 自动 ACTIVE
        await start;

        Assert.Equal(PluginLifecycleState.Active, runtime.State);
        Assert.Equal(1, plugin.InitializeCount);
        Assert.Contains(PluginLifecycleState.Loading, states);
        Assert.Contains(PluginLifecycleState.Active, states);
    }

    [Fact]
    public async Task Dependency_disappearance_stops_dependent_plugin()
    {
        var (store, discovery) = CreateDiscovery();
        store.Provide("fs", string.Empty, new object(), "provider-x");
        var plugin = new FakePlugin();
        var runtime = new PluginRuntime(
            new PluginManifest("p", "1.0.0", "P.cs", ["cordis-runtime"], [], ["fs"]),
            _ => plugin,
            discovery,
            _ => new Context.ContextFacade("p"));
        await runtime.StartAsync();
        Assert.Equal(PluginLifecycleState.Active, runtime.State);

        store.Remove("fs", string.Empty, "provider-x"); // 值删 → 不可用 → 依赖方卸载（ADR-0007，事件驱动异步）

        Assert.True(
            await WaitUntilAsync(() => runtime.State == PluginLifecycleState.Disposed, TimeSpan.FromSeconds(2)),
            "依赖消失后依赖方应卸载");
        Assert.Equal(1, plugin.DisposeCount);
    }

    [Fact]
    public async Task Initialize_failure_enters_failed_and_restart_recovers()
    {
        var (_, discovery) = CreateDiscovery();
        var plugin = new FakePlugin(failFirstInitialize: true);
        var runtime = new PluginRuntime(
            new PluginManifest("p", "1.0.0", "P.cs", ["cordis-runtime"], [], []),
            _ => plugin,
            discovery,
            _ => new Context.ContextFacade("p"));

        await runtime.StartAsync();

        Assert.Equal(PluginLifecycleState.Failed, runtime.State);
        await Assert.ThrowsAsync<Keystone.Core.Errors.KeystoneException>(() => runtime.AwaitAsync());

        plugin.FailFirstInitialize = false;
        await runtime.RestartAsync();

        Assert.Equal(PluginLifecycleState.Active, runtime.State);
        Assert.Equal(2, plugin.InitializeCount);
    }

    [Fact]
    public async Task Stop_runs_quiesce_disposes_effects_and_clears_service_registration()
    {
        var root = new Context.ContextFacade("root");
        var store = root.Services; // facade 链共享 root 的值层 store——discovery 投影同一份
        var runtime = new PluginRuntime(
            new PluginManifest("p", "1.0.0", "P.cs", ["cordis-runtime"], ["fs"], []),
            _ => new ProvidingPlugin("fs"), // 值即注册：provides 声明须 init 期 ctx.Provide 兑现
            new InMemoryServiceDiscovery(store),
            _ => new Context.ContextFacade("p", root));
        await runtime.StartAsync();
        Assert.True(store.IsAvailable("fs", string.Empty));

        var disposed = false;
        runtime.Context!.Context.Effect(() =>
        {
            disposed = true;
            return Task.CompletedTask;
        }, label: "cleanup");

        await runtime.StopAsync();

        Assert.Equal(PluginLifecycleState.Disposed, runtime.State);
        Assert.True(disposed, "quiesce 应执行 effect disposer（逆序收敛）");
        Assert.False(store.IsAvailable("fs", string.Empty), "卸载后值即摘除（可用性随值消失）");
        await using var _ = runtime;
    }

    [Fact]
    public async Task Slow_disposer_hits_quiesce_timeout_and_forces_dispose()
    {
        var (_, discovery) = CreateDiscovery();
        var plugin = new FakePlugin();
        var runtime = new PluginRuntime(
            new PluginManifest("p", "1.0.0", "P.cs", ["cordis-runtime"], [], []),
            _ => plugin,
            discovery,
            _ => new Context.ContextFacade("p"),
            quiesceTimeout: TimeSpan.FromMilliseconds(50));
        await runtime.StartAsync();

        runtime.Context!.Context.Effect(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
        }, label: "slow");

        await runtime.StopAsync(); // 超时强制 dispose，不无限等待（ADR-0005 风险缓解）

        Assert.Equal(PluginLifecycleState.Disposed, runtime.State);
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(20);
        }

        return condition();
    }

    private sealed class ProvidingPlugin(string serviceName) : IPlugin
    {
        public int DisposeCount { get; private set; }

        public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
        {
            context.Provide(serviceName, new object());
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            DisposeCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePlugin : IPlugin
    {
        private bool _failFirst;

        public FakePlugin(bool failFirstInitialize = false)
        {
            _failFirst = failFirstInitialize;
        }

        public bool FailFirstInitialize
        {
            get => _failFirst;
            set => _failFirst = value;
        }

        public int InitializeCount { get; private set; }

        public int DisposeCount { get; private set; }

        public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
        {
            InitializeCount++;
            if (_failFirst)
            {
                throw new InvalidOperationException("init failed");
            }

            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            DisposeCount++;
            return Task.CompletedTask;
        }
    }
}
