using Keystone.Runtime.Context;
using Keystone.Runtime.Plugins.Lifecycle;
using Keystone.Runtime.Plugins.Manifest;
using Keystone.Runtime.Plugins.Services;

namespace Keystone.Runtime.Tests;

public class ServiceRegistryTests
{
    [Fact]
    public void Register_makes_service_available_and_raises_event()
    {
        var registry = new ServiceRegistry();
        var changes = new List<string>();
        registry.ServiceAvailabilityChanged += (_, args) => changes.Add($"{args.ServiceName}:{args.Available}");

        registry.Register("fs", providerId: "plugin-a");

        Assert.True(registry.IsAvailable("fs"));
        Assert.Equal(["fs:True"], changes);
    }

    [Fact]
    public void Unregister_raises_event_and_removes_availability()
    {
        var registry = new ServiceRegistry();
        registry.Register("fs", "plugin-a");
        var changes = new List<string>();
        registry.ServiceAvailabilityChanged += (_, args) => changes.Add($"{args.ServiceName}:{args.Available}");

        registry.Unregister("fs", "plugin-a");

        Assert.False(registry.IsAvailable("fs"));
        Assert.Equal(["fs:False"], changes);
    }

    [Fact]
    public void Unregister_other_provider_does_not_affect_availability()
    {
        var registry = new ServiceRegistry();
        registry.Register("fs", "plugin-a");

        registry.Unregister("fs", "plugin-b"); // 非提供者注销 → 无影响

        Assert.True(registry.IsAvailable("fs"));
    }
}

public class PluginRuntimeTests
{
    private static ServiceRegistry CreateRegistry() => new();

    [Fact]
    public async Task Missing_dependency_holds_pending_until_service_appears()
    {
        var registry = CreateRegistry();
        var plugin = new FakePlugin();
        var states = new List<PluginLifecycleState>();
        var runtime = new PluginRuntime(
            new PluginManifest("p", "1.0.0", "P.cs", ["cordis-runtime"], [], ["fs"]),
            _ => plugin,
            registry,
            _ => new Context.ContextFacade("p"));

        runtime.StateChanged += (_, args) => states.Add(args.State);

        var start = runtime.StartAsync();
        Assert.Equal(PluginLifecycleState.Pending, runtime.State); // 依赖缺失 → PENDING

        registry.Register("fs", "provider-x"); // 依赖出现 → 自动 ACTIVE
        await start;

        Assert.Equal(PluginLifecycleState.Active, runtime.State);
        Assert.Equal(1, plugin.InitializeCount);
        Assert.Contains(PluginLifecycleState.Loading, states);
        Assert.Contains(PluginLifecycleState.Active, states);
    }

    [Fact]
    public async Task Dependency_disappearance_stops_dependent_plugin()
    {
        var registry = CreateRegistry();
        registry.Register("fs", "provider-x");
        var plugin = new FakePlugin();
        var runtime = new PluginRuntime(
            new PluginManifest("p", "1.0.0", "P.cs", ["cordis-runtime"], [], ["fs"]),
            _ => plugin,
            registry,
            _ => new Context.ContextFacade("p"));
        await runtime.StartAsync();
        Assert.Equal(PluginLifecycleState.Active, runtime.State);

        registry.Unregister("fs", "provider-x"); // 依赖消失 → 依赖方卸载（ADR-0007，事件驱动异步）

        Assert.True(
            await WaitUntilAsync(() => runtime.State == PluginLifecycleState.Disposed, TimeSpan.FromSeconds(2)),
            "依赖消失后依赖方应卸载");
        Assert.Equal(1, plugin.DisposeCount);
    }

    [Fact]
    public async Task Initialize_failure_enters_failed_and_restart_recovers()
    {
        var registry = CreateRegistry();
        var plugin = new FakePlugin(failFirstInitialize: true);
        var runtime = new PluginRuntime(
            new PluginManifest("p", "1.0.0", "P.cs", ["cordis-runtime"], [], []),
            _ => plugin,
            registry,
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
        var registry = CreateRegistry();
        var plugin = new FakePlugin();
        var runtime = new PluginRuntime(
            new PluginManifest("p", "1.0.0", "P.cs", ["cordis-runtime"], ["fs"], []),
            _ => plugin,
            registry,
            _ => new Context.ContextFacade("p"));
        await runtime.StartAsync();
        Assert.True(registry.IsAvailable("fs"));

        var disposed = false;
        runtime.Context!.Context.Effect(() =>
        {
            disposed = true;
            return Task.CompletedTask;
        }, label: "cleanup");

        await runtime.StopAsync();

        Assert.Equal(PluginLifecycleState.Disposed, runtime.State);
        Assert.True(disposed, "quiesce 应执行 effect disposer（逆序收敛）");
        Assert.False(registry.IsAvailable("fs"), "卸载后应摘除服务注册");
        Assert.Equal(1, plugin.DisposeCount);
    }

    [Fact]
    public async Task Slow_disposer_hits_quiesce_timeout_and_forces_dispose()
    {
        var registry = CreateRegistry();
        var plugin = new FakePlugin();
        var runtime = new PluginRuntime(
            new PluginManifest("p", "1.0.0", "P.cs", ["cordis-runtime"], [], []),
            _ => plugin,
            registry,
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
