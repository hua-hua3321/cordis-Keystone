using Keystone.Runtime.Context;
using Keystone.Runtime.Plugins.Lifecycle;
using Keystone.Runtime.Plugins.Manifest;
using Keystone.Runtime.Plugins.Services;

namespace Keystone.Runtime.Tests;

/// <summary>
/// P66（19 号审计 P1-1..5 + D-7 + P2-13 + P2-16）：状态机竞态 + 门控 ACTIVE 时机。
/// P1-1 AwaitAsync 死字段（_settled 从未赋值——Pending 期等待立即返回）；
/// P1-2 PENDING 期 Stop 不能取消依赖等待（超时后延迟翻 FAILED）；
/// P1-3 StopCoreAsync 无重入守卫（并发双 dispose）；
/// P1-5 Loading 期依赖消失带缺失依赖进入 ACTIVE；
/// P2-13 依赖消失 → PENDING（可 re-arm 存活态，对齐 fiber.ts:611-623）；
/// D-7 提供者须 ACTIVE 才放行依赖方（init 中途 provide 不通知——对齐 reflect.ts:294-296）；
/// P2-16 provides 兑现校验须查属主（他人提供同名值不得蒙混）。
/// </summary>
public class StateMachineGatingRaceTests
{
    private static PluginManifest Manifest(
        string id,
        IReadOnlyList<string>? provides = null,
        IReadOnlyList<string>? inject = null)
        => new(id, "1.0.0", "P.cs", ["cordis-runtime"], provides ?? [], inject ?? []);

    private sealed class FakePlugin : IPlugin
    {
        public int DisposeCount;

        public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
            => Task.CompletedTask;

        public Task DisposeAsync()
        {
            Interlocked.Increment(ref DisposeCount);
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

    /// <summary>init 阻塞在门闩上的 provider（Provide 先行——D-7 窗口构造）。</summary>
    private sealed class GatedProvidingPlugin(string serviceName, Task gate) : IPlugin
    {
        public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
        {
            context.Provide(serviceName, new object());
            return gate; // 到 ACTIVE 前 Provide 已发生
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }

    private static (InMemoryServiceDiscovery Discovery, Func<string, IPluginContext> Factory) Layer(ContextFacade root)
        => (new InMemoryServiceDiscovery(root.Services), id => new ContextFacade(id, root));

    // ── P1-1：AwaitAsync 真等待（Pending → Active 完成才返回）──

    [Fact]
    public async Task AwaitAsync_waits_until_terminal_state()
    {
        // PENDING 等待场景（依赖缺失 → 长超时）：AwaitAsync 必须真等待到落定
        var root = new ContextFacade("root");
        var (discovery, factory) = Layer(root);
        var dependent = new PluginRuntime(
            Manifest("dep", inject: ["fs"]),
            _ => new FakePlugin(),
            discovery,
            factory,
            dependencyTimeout: TimeSpan.FromSeconds(30));

        var start = dependent.StartAsync(); // PENDING（等依赖）
        var waiting = dependent.AwaitAsync();
        await Task.Delay(100);

        Assert.False(waiting.IsCompleted); // PENDING 期不得提前返回（修复前 _settled 死字段立即完成）
        Assert.False(start.IsCompleted);

        var provider = new PluginRuntime( // 依赖到位 → 落定
            Manifest("provider", provides: ["fs"]),
            _ => new ProvidingPlugin("fs"),
            discovery,
            factory,
            dependencyTimeout: TimeSpan.FromSeconds(30));
        await provider.StartAsync();

        await waiting; // ACTIVE 后完成
        await start;
        Assert.Equal(PluginLifecycleState.Active, dependent.State);
        await dependent.DisposeAsync();
        await provider.DisposeAsync();
    }

    // ── P1-2：PENDING 期 Stop 取消依赖等待——不延迟翻 FAILED ──

    [Fact]
    public async Task Stop_during_pending_wait_does_not_flip_failed()
    {
        var root = new ContextFacade("root");
        var (discovery, factory) = Layer(root);
        var runtime = new PluginRuntime(
            Manifest("dep", inject: ["missing"]),
            _ => new FakePlugin(),
            discovery,
            factory,
            dependencyTimeout: TimeSpan.FromMilliseconds(200));

        var start = runtime.StartAsync(); // PENDING（等依赖）
        await Task.Delay(30);
        await runtime.StopAsync(); // 停止（终态意图）

        await start; // 启动任务收敛
        await Task.Delay(400); // 越过依赖超时窗口

        Assert.Equal(PluginLifecycleState.Disposed, runtime.State); // 不得延迟翻 FAILED（修复前超时后变 Failed）
        await runtime.DisposeAsync();
    }

    // ── P1-3：并发 Stop 恰一次插件 dispose ──

    [Fact]
    public async Task Concurrent_stops_dispose_plugin_exactly_once()
    {
        var root = new ContextFacade("root");
        var (discovery, factory) = Layer(root);
        var plugin = new FakePlugin();
        var runtime = new PluginRuntime(
            Manifest("p"),
            _ => plugin,
            discovery,
            factory);

        await runtime.StartAsync();
        var stop1 = runtime.StopAsync();
        var stop2 = runtime.StopAsync(); // 并发第二停（修复前双 quiesce/双 dispose）

        await Task.WhenAll(stop1, stop2);
        Assert.Equal(1, plugin.DisposeCount); // 恰一次
        await runtime.DisposeAsync();
    }

    // ── P1-5 + P2-13：Loading 期依赖消失 → 加载完成后卸载至 PENDING（可 re-arm）──

    [Fact]
    public async Task Dependency_loss_during_loading_unloads_to_pending()
    {
        var root = new ContextFacade("root");
        var (discovery, factory) = Layer(root);
        var provider = new PluginRuntime(
            Manifest("provider", provides: ["fs"]),
            _ => new ProvidingPlugin("fs"),
            discovery,
            factory);
        await provider.StartAsync();

        var initGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slowPlugin = new SlowInitPlugin(initGate.Task);
        var dependent = new PluginRuntime(
            Manifest("dep", inject: ["fs"]),
            _ => slowPlugin,
            discovery,
            factory);
        var start = dependent.StartAsync();
        await slowPlugin.EnteredInit.Task; // 依赖方进入 init（Loading）

        await provider.DisposeAsync(); // 依赖消失（Loading 期）
        initGate.TrySetResult(); // 放行 init 完成
        await start;

        await WaitForAsync(() => dependent.State is PluginLifecycleState.Pending or PluginLifecycleState.Disposed);
        Assert.Equal(PluginLifecycleState.Pending, dependent.State); // P2-13：可 re-arm 存活态（修复前 Active / Disposed）
        Assert.True(slowPlugin.DisposeCount >= 1); // 已卸载（修复前带缺失依赖 ACTIVE）
        await dependent.DisposeAsync();
    }

    // ── P2-13：依赖消失 → PENDING；重现 → 自动重启 ──

    [Fact]
    public async Task Dependency_loss_lands_pending_and_reappearance_restarts()
    {
        var root = new ContextFacade("root");
        var (discovery, factory) = Layer(root);
        var provider = new PluginRuntime(
            Manifest("provider", provides: ["fs"]),
            _ => new ProvidingPlugin("fs"),
            discovery,
            factory);
        await provider.StartAsync();

        var dependent = new PluginRuntime(
            Manifest("dep", inject: ["fs"]),
            _ => new FakePlugin(),
            discovery,
            factory);
        await dependent.StartAsync();
        Assert.Equal(PluginLifecycleState.Active, dependent.State);

        await provider.DisposeAsync(); // 依赖消失
        await WaitForAsync(() => dependent.State == PluginLifecycleState.Pending);
        Assert.Equal(PluginLifecycleState.Pending, dependent.State); // 非 Disposed（存活可 re-arm）

        var provider2 = new PluginRuntime( // 依赖重现
            Manifest("provider2", provides: ["fs"]),
            _ => new ProvidingPlugin("fs"),
            discovery,
            factory);
        await provider2.StartAsync();

        await WaitForAsync(() => dependent.State == PluginLifecycleState.Active);
        Assert.Equal(PluginLifecycleState.Active, dependent.State); // re-arm 重启
        await dependent.DisposeAsync();
        await provider2.DisposeAsync();
    }

    // ── D-7：提供者 ACTIVE 前不释放依赖方 ──

    [Fact]
    public async Task Provider_mid_init_provide_does_not_release_dependent()
    {
        var root = new ContextFacade("root");
        var (discovery, factory) = Layer(root);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new PluginRuntime(
            Manifest("provider", provides: ["fs"]),
            _ => new GatedProvidingPlugin("fs", gate.Task),
            discovery,
            factory,
            dependencyTimeout: TimeSpan.FromSeconds(30));

        var dependent = new PluginRuntime(
            Manifest("dep", inject: ["fs"]),
            _ => new FakePlugin(),
            discovery,
            factory,
            dependencyTimeout: TimeSpan.FromSeconds(30));

        var providerStart = provider.StartAsync();
        await Task.Delay(100); // provider 已 Provide 但仍 LOADING（门未开）

        var depStart = dependent.StartAsync();
        await Task.Delay(150);
        Assert.Equal(PluginLifecycleState.Pending, dependent.State); // D-7：不得放行（修复前短暂 Active）

        gate.TrySetResult(); // provider 收敛 → ACTIVE → 补发通知
        await providerStart;
        await depStart;
        Assert.Equal(PluginLifecycleState.Active, dependent.State); // ACTIVE 后放行
        await dependent.DisposeAsync();
        await provider.DisposeAsync();
    }

    // ── P2-16：provides 兑现须属主本人 ──

    [Fact]
    public async Task Provides_fulfillment_requires_owner()
    {
        var root = new ContextFacade("root");
        var (discovery, factory) = Layer(root);

        // 他人先占同名服务（默认域）
        var squatter = new PluginRuntime(
            Manifest("squatter", provides: ["svc"]),
            _ => new ProvidingPlugin("svc"),
            discovery,
            factory);
        await squatter.StartAsync();

        // 声明 provides svc 但 init 不 Provide → 必须 FAILED（修复前：他人值蒙混通过 IsAvailable 检查）
        var faker = new PluginRuntime(
            Manifest("faker", provides: ["svc"]),
            _ => new FakePlugin(),
            discovery,
            factory);
        await faker.StartAsync();

        Assert.Equal(PluginLifecycleState.Failed, faker.State);
        await faker.DisposeAsync();
        await squatter.DisposeAsync();
    }

    private sealed class SlowInitPlugin(Task gate) : IPlugin
    {
        public TaskCompletionSource EnteredInit { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int DisposeCount;

        public async Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
        {
            EnteredInit.TrySetResult();
            await gate;
        }

        public Task DisposeAsync()
        {
            Interlocked.Increment(ref DisposeCount);
            return Task.CompletedTask;
        }
    }

    private static async Task WaitForAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                Assert.Fail("condition not met within timeout");
            }

            await Task.Delay(20);
        }
    }
}
