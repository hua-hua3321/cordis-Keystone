using Keystone.Runtime.Plugins.Lifecycle;
using Keystone.Runtime.Plugins.Loading;
using Keystone.Runtime.Plugins.Manifest;

namespace Keystone.Hosting.Tests;

/// <summary>
/// P69/D-1（19 号审计 LD-6，对齐 fiber.ts update()→restart() 语义）：真热更新——
/// config-only 变更走同 ALC 原地重启（新插件实例、旧程序集）：不重编译、不换 ALC、
/// 源码坏时热更不受影响。修复前"热路径"内部仍 ReloadPluginAsync（重编译+新 ALC+quiesce）：
/// config 变更即重编译、源码坏 → 热更必失败。
/// 冷路径（结构变/源码变）仍走冷重启（新 ALC）——分级不变。
/// </summary>
[Collection("ConfigInjection")]
public class TrueHotUpdateTests
{
    /// <summary>独立插件类型（刻意不复用 VersionedPlugin）：跨 ALC 静态读取按类型名聚合，
    /// 共名会在并行套件中泄漏他测试的 ALC 副本计数（P57-T4 同类 flake）。</summary>
    public const string InPlaceSource = """
        using System;
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        public sealed class InPlacePlugin : IPlugin
        {
            public static string? LastConfig;
            public static int BootCount;

            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
            {
                BootCount++;
                LastConfig = System.Text.Json.JsonSerializer.Serialize(config);
                return Task.CompletedTask;
            }

            public Task DisposeAsync() => Task.CompletedTask;
        }
        """;

    private static KeystoneHostOptions Options() => new()
    {
        ManifestProvider = _ => new PluginManifest("hot", "1.0.0", "InPlace.cs", ["cordis-runtime"], [], []),
        SourceProvider = _ => new PluginSource("hot", InPlaceSource),
    };

    [Fact]
    public async Task Config_update_succeeds_even_when_source_is_broken()
    {
        // D-1 头号断言：热更新不依赖源码（对齐 Cordis update——同代码 restart）
        var options = Options();
        await using var host = new KeystoneHost(options);
        await host.StartAsync("""
            - id: hot
              name: ./plugins/hot
              config:
                mode: v1
            """);

        // 启动后源码"坏了"（获取端将返回损坏源）——config 热更新不得触碰源码
        options.SourceProvider = _ => new PluginSource("hot", "public class {");

        await host.UpdatePluginAsync("hot", new Dictionary<string, object?> { ["mode"] = "v2" });

        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("hot")); // 修复前：重编译损坏源 → FAILED
        Assert.Equal("v2", ((Dictionary<string, object?>)host.ResolveEntry("hot").Config!)["mode"]);
        await host.ShutdownAsync();
    }

    [Fact]
    public async Task Config_update_restarts_in_place_on_same_assembly()
    {
        // 同 ALC 原地重启：静态状态在同一程序集上累积（非新 ALC 从零）
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("""
            - id: hot
              name: ./plugins/hot
              config:
                mode: v1
            """);

        var bootsBefore = ForceGcAndReadBoot();
        await host.UpdatePluginAsync("hot", new Dictionary<string, object?> { ["mode"] = "v2" });

        var bootsAfter = ForceGcAndReadBoot();
        Assert.Equal(bootsBefore + 1, bootsAfter); // 同程序集累积 +1（冷重启则新 ALC 从 1 起）
        Assert.Contains(ReadLastConfigs(), c => c?.Contains("\"mode\":\"v2\"", StringComparison.Ordinal) ?? false);
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("hot"));
        await host.ShutdownAsync();
    }

    [Fact]
    public async Task UpdateEntry_config_only_also_takes_in_place_path()
    {
        // CA-4 组合更新热分支同样走原地通道（源坏不受影响）
        var options = Options();
        await using var host = new KeystoneHost(options);
        await host.StartAsync("""
            - id: hot
              name: ./plugins/hot
              config:
                mode: v1
            """);

        options.SourceProvider = _ => new PluginSource("hot", "public class {");
        await host.UpdateEntryAsync("hot", new Keystone.Config.Entries.EntryOptions
        {
            Config = new Dictionary<string, object?> { ["mode"] = "v2" },
        });

        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("hot"));
        Assert.Equal("v2", ((Dictionary<string, object?>)host.ResolveEntry("hot").Config!)["mode"]);
        await host.ShutdownAsync();
    }

    [Fact]
    public async Task Structural_change_still_takes_cold_path_with_new_alc()
    {
        // 分级不变：结构键变 → 冷重启（重编译 + 新 ALC）
        await using var host = new KeystoneHost(Options());
        var reloads = new List<string>();
        host.PluginReloading += (_, e) => reloads.Add(e.EntryId);
        await host.StartAsync("""
            - id: hot
              name: ./plugins/hot
              config:
                mode: v1
            """);

        await host.UpdateEntryAsync("hot", new Keystone.Config.Entries.EntryOptions
        {
            Name = "./plugins/hot2", // 结构变（name 变）
        });

        Assert.Contains("hot", reloads); // 冷重启事件
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("hot"));
        await host.ShutdownAsync();
    }

    // ── 静态读取（跨 ALC 收集全部副本；GC 压实后单副本）──

    private static int ForceGcAndReadBoot()
    {
        HotReloadTests.ForceGc();
        return HotReloadTests.ReadStaticInt("InPlacePlugin", "BootCount");
    }

    private static List<string?> ReadLastConfigs()
        => HotReloadTests.ReadStaticStrings("InPlacePlugin", "LastConfig");
}
