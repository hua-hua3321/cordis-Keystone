using Keystone.Runtime.Plugins.Lifecycle;
using Keystone.Runtime.Plugins.Loading;
using Keystone.Runtime.Plugins.Manifest;
using Keystone.Runtime.Plugins.Services;

namespace Keystone.Runtime.Tests;

public class PluginLoaderTests
{
    private static PluginManifest SampleManifest(string id = "sample")
        => new(id, "1.0.0", "SamplePlugin.cs", ["cordis-runtime"], [], []);

    [Fact]
    public async Task Load_compiles_loads_instantiates_and_runs()
    {
        await using var loader = await PluginLoader.CreateAsync(
            new PluginSource("sample", SampleSources.V1),
            SampleManifest(),
            new ServiceRegistry(),
            id => new Context.ContextFacade(id));

        Assert.NotNull(loader.Runtime);
        Assert.Equal(PluginLifecycleState.Active, loader.Runtime.State);
    }

    [Fact]
    public async Task Dispose_stops_runtime_and_collects_alc()
    {
        // 两段式：创建/释放在一个方法（返回后局部引用全部出作用域），GC 断言在另一个方法——
        // Debug 构建下局部变量存活到方法结束，同方法内断言会因 JIT 保留引用而失败
        var weak = await CreateAndDisposeLoaderAsync();
        Assert.NotNull(weak);

        ForceGc();

        Assert.False(weak.IsAlive, "卸载后 ALC 应可回收（无残留引用）");
    }

    private static async Task<WeakReference?> CreateAndDisposeLoaderAsync()
    {
        var loader = await PluginLoader.CreateAsync(
            new PluginSource("sample", SampleSources.V1),
            SampleManifest(),
            new ServiceRegistry(),
            id => new Context.ContextFacade(id));

        await loader.DisposeAsync();
        return loader.UnloadedAlcReference;
    }

    [Fact]
    public async Task Hot_reload_replaces_old_version_and_collects_old_alc()
    {
        var loader = await PluginLoader.CreateAsync(
            new PluginSource("sample", SampleSources.V1),
            SampleManifest(),
            new ServiceRegistry(),
            id => new Context.ContextFacade(id));
        await using var _ = loader;

        await loader.ReloadAsync(new PluginSource("sample", SampleSources.V2));
        var oldWeak = loader.UnloadedAlcReference;
        Assert.NotNull(oldWeak);
        ForceGc();

        Assert.True(loader.Runtime.State is PluginLifecycleState.Active, "新版本应 ACTIVE");
        Assert.False(oldWeak.IsAlive, "旧版本 ALC 应可回收");

        // 新版本类型已加载（Version = v2）
        var version = loader.GetPluginVersion();
        Assert.Equal("v2", version);
    }

    [Fact]
    public async Task Compile_failure_reports_lifecycle_load_failed()
    {
        await Assert.ThrowsAsync<Keystone.Core.Errors.KeystoneException>(async () =>
            await PluginLoader.CreateAsync(
                new PluginSource("broken", "public class {"),
                SampleManifest(),
                new ServiceRegistry(),
                id => new Context.ContextFacade(id)));
    }

    private static void ForceGc()
    {
        // ALC.Unload 是排队卸载：需要多轮 GC + 终结器等待才能完成回收（收集性 ALC 经验值）
        for (var i = 0; i < 10; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Thread.Sleep(20);
        }
    }
}
