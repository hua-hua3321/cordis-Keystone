using Keystone.Config.Entries;
using Keystone.Runtime.Plugins.Lifecycle;

namespace Keystone.Hosting.Tests;

/// <summary>
/// CA-4 组合 update（18 §2 P1，P59）：一次调用改选项 + 跨组移动 + position。
/// 修复前只有分离的 MoveEntryAsync（回滚仅回根——原位置信息丢失）与 UpdatePluginAsync（仅 config）。
/// 兑现：UpdateEntryAsync —— 结构键(name/inject/isolate)与 parent 均不变 → 热更（PatchContext 瀑布）；
/// 结构变或跨组 → 冷重启；移动记账 (源组, 原下标)，任一步失败回插原位置（精确下标）。
/// </summary>
public class UpdateEntryTests
{
    private static KeystoneHostOptions Options() => new()
    {
        ManifestProvider = e => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
            e.Id!, "1.0.0", "X.cs", ["cordis-runtime"], [], []),
        SourceProvider = e => new Keystone.Runtime.Plugins.Loading.PluginSource(
            e.Id!, e.Name == "./plugins/bad" ? "public class {" : HostTestSources.DependentSource),
    };

    [Fact]
    public async Task UpdateEntry_moves_and_updates_config_in_one_call()
    {
        // 移动 + config 同改一步成功：条目入新组 + config 生效 + 保持 Active
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("""
            - id: src
              name: ./plugins/src
              group:
                - id: a
                  name: ./plugins/a
                  config:
                    mode: v1
            - id: dst
              name: ./plugins/dst
              group: []
            """);

        await host.UpdateEntryAsync("a", new EntryOptions
        {
            Id = "a",
            Name = "./plugins/a",
            Config = new Dictionary<string, object?> { ["mode"] = "v2" },
        }, parent: "dst");

        var dst = host.DumpConfig().Single(e => e.Id == "dst");
        Assert.Contains(dst.Group!, c => c.Id == "a"); // 已移动
        Assert.Equal("v2", ((Dictionary<string, object?>)host.ResolveEntry("a").Config!)["mode"]); // config 已更
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("a")); // 插件保持运行

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task UpdateEntry_config_only_takes_hot_path()
    {
        // 仅 config 变 + 无移动 → 热更（无冷重启事件）
        await using var host = new KeystoneHost(Options());
        var reloads = 0;
        host.PluginReloading += (_, _) => reloads++;
        await host.StartAsync("""
            - id: a
              name: ./plugins/a
              config:
                mode: v1
            """);

        await host.UpdateEntryAsync("a", new EntryOptions
        {
            Id = "a",
            Name = "./plugins/a",
            Config = new Dictionary<string, object?> { ["mode"] = "v2" },
        });

        Assert.Equal(0, reloads); // 热路径
        Assert.Equal("v2", ((Dictionary<string, object?>)host.ResolveEntry("a").Config!)["mode"]);
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("a"));

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task UpdateEntry_structural_change_takes_cold_path()
    {
        // inject 变（结构键变）→ 冷重启
        await using var host = new KeystoneHost(Options());
        var reloads = new List<string>();
        host.PluginReloading += (_, e) => reloads.Add(e.EntryId);
        await host.StartAsync("""
            - id: a
              name: ./plugins/a
            """);

        await host.UpdateEntryAsync("a", new EntryOptions
        {
            Id = "a",
            Name = "./plugins/a2", // name 变 → 结构键变
        });

        Assert.Contains("a", reloads); // 冷路径
        Assert.Equal("./plugins/a2", host.ResolveEntry("a").Name);

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task UpdateEntry_failure_restores_original_position()
    {
        // 失败回插原位置（精确下标）——修复 MoveEntryAsync 回滚只回根的偏差：
        // 移动成功后冷重启失败 → 条目回源组原下标 1（非根、非源组尾部）
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("""
            - id: src
              name: ./plugins/src
              group:
                - id: keep0
                  name: ./plugins/keep0
                - id: a
                  name: ./plugins/a
                - id: keep2
                  name: ./plugins/keep2
            - id: dst
              name: ./plugins/dst
              group: []
            """);

        // name 变（结构键变→冷路径）+ 指向 bad 源（编译失败）→ 冷重启失败 → 回滚
        await Assert.ThrowsAnyAsync<Exception>(() => host.UpdateEntryAsync("a", new EntryOptions
        {
            Id = "a",
            Name = "./plugins/bad",
        }, parent: "dst"));

        var src = host.DumpConfig().Single(e => e.Id == "src");
        Assert.Equal(["keep0", "a", "keep2"], src.Group!.Select(c => c.Id)); // 原下标精确恢复

        await host.ShutdownAsync();
    }
}
