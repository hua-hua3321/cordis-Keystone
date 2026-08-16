using System.Diagnostics;
using Keystone.Core;
using Keystone.Runtime.Plugins.Lifecycle;

namespace Keystone.Hosting.Tests;

/// <summary>
/// P72-T2（框架配置启动冻结契约）：KeystoneHostOptions.FrameworkSettings 在 StartAsync 时
/// 快照一次，启动后嵌入方修改 options 对象不再影响任何后续运行时（冷/热/后加一致）。
/// 观察量：quiesce 超时——热更新触发旧实例收敛，冻结值（10s，等 6s 慢 dispose 自然完成）
/// 与启动后修改值（50ms 强制收敛）在 UpdatePluginAsync 耗时上可区分。
/// </summary>
public class FrameworkSettingsFreezeTests
{
    private const string SlowDisposeSource = """
        using System;
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        public sealed class SlowDisposePlugin : IPlugin
        {
            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
                => Task.CompletedTask;

            public Task DisposeAsync() => Task.Delay(TimeSpan.FromSeconds(6));
        }
        """;

    private static KeystoneHostOptions Options() => new()
    {
        // 默认 FrameworkSettings：QuiesceTimeout = 10s（启动时应定格此值）
        ManifestProvider = e => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
            e.Id!, "1.0.0", "X.cs", ["cordis-runtime"], [], []),
        SourceProvider = e => new Keystone.Runtime.Plugins.Loading.PluginSource(e.Id!, SlowDisposeSource),
    };

    [Fact]
    public async Task Framework_settings_snapshot_at_startup_not_mutated_live()
    {
        var options = Options();
        await using var host = new KeystoneHost(options);
        await host.StartAsync("- id: slowdispose\n  name: ./slowdispose\n"); // 快照点：QuiesceTimeout = 10s
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("slowdispose"));

        // 启动后嵌入方修改 options（病理操作）——契约：之后任何路径创建的 runtime 都用启动快照
        options.FrameworkSettings = new KeystoneSettings { QuiesceTimeout = TimeSpan.FromMilliseconds(50) };

        // 冷重载：重建 loader/runtime（修复前此处读活 options 的 50ms → 后续收敛被 50ms 强制）
        await host.ReloadPluginAsync("slowdispose");
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("slowdispose"));

        var stopwatch = Stopwatch.StartNew();
        await host.UpdatePluginAsync(
            "slowdispose", new Dictionary<string, object?> { ["k"] = "v" }, save: false); // D-1：旧实例 quiesce
        stopwatch.Stop();

        // 冻结（10s）：等 6s 慢 dispose 自然完成 ≈ 6s；读到修改值（50ms）：强制收敛 ≈ 0.1s
        Assert.True(stopwatch.Elapsed > TimeSpan.FromSeconds(3),
            $"quiesce 应使用启动快照（实际 {stopwatch.Elapsed}——冷重载读到了启动后修改的 50ms）");
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("slowdispose")); // 新实例照常 ACTIVE
        await host.ShutdownAsync();
    }
}
