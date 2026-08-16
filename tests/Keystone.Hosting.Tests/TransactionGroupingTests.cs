using Keystone.Config.Entries;
using Keystone.Core.Errors;
using Keystone.Runtime.Plugins.Lifecycle;

namespace Keystone.Hosting.Tests;

/// <summary>
/// P64 修复批（19 号审计 P0-1/2/3 + D-4/D-5）：CA-3 事务三连缺陷 + Cordis 对齐裁定。
/// P0-1：diff Added 丢失组归属（向既有组新增子叶被插到根）。
/// P0-2：新增带子组必然失败（组 Create 已加载子，子再 Create 撞 duplicate id）。
/// P0-3：结构步中途失败留半应用态（undo 迟登记）。
/// D-5：Removed 不回滚（Cordis group.ts:95-101 全量重建含 Removed——P59 注记作废）。
/// D-4：失败复原运行时（对齐 entry.ts:232-243 重启旧插件）。
/// </summary>
public class TransactionGroupingTests
{
    /// <summary>按 name 键控源："-v2" 名 = 损坏源（结构变 → 重载编译失败注入）。</summary>
    private static KeystoneHostOptions Options() => new()
    {
        ManifestProvider = e => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
            e.Id!, "1.0.0", "X.cs", ["cordis-runtime"], [], []),
        SourceProvider = e => new Keystone.Runtime.Plugins.Loading.PluginSource(
            e.Id!,
            (e.Name ?? "").EndsWith("-v2", StringComparison.Ordinal) || (e.Name ?? "").Contains("nonexistent", StringComparison.Ordinal)
                ? "public class {" // 编译失败 → 冷重启失败注入
                : HostTestSources.DependentSource),
    };

    private static async Task<KeystoneHost> StartAsync(string yaml)
    {
        var host = new KeystoneHost(Options());
        await host.StartAsync(yaml);
        return host;
    }

    // ── P0-1：向既有组新增子叶 → 子叶必须进组（不被提到根）──

    [Fact]
    public async Task Added_child_goes_into_existing_group()
    {
        await using var host = await StartAsync("""
            - id: g
              name: ./g
              group:
                - id: existing
                  name: ./existing
            """);

        await host.ApplyConfigAsync(EntryParser.Parse("""
            - id: g
              name: ./g
              group:
                - id: existing
                  name: ./existing
                - id: added
                  name: ./added
            """));

        var group = Assert.Single(host.DumpConfig());
        Assert.Equal("g", group.Id);
        Assert.Equal(["existing", "added"], group.Group!.Select(e => e.Id)); // 树形归属保持

        await host.ShutdownAsync();
    }

    // ── P0-2：全新组+子 → 必须成功（子不撞 duplicate id）──

    [Fact]
    public async Task Added_group_with_children_succeeds()
    {
        await using var host = await StartAsync("- id: solo\n  name: ./solo\n");

        await host.ApplyConfigAsync(EntryParser.Parse("""
            - id: solo
              name: ./solo
            - id: newg
              name: ./newg
              group:
                - id: c1
                  name: ./c1
                - id: c2
                  name: ./c2
            """));

        var newg = host.DumpConfig().Single(e => e.Id == "newg");
        Assert.Equal(["c1", "c2"], newg.Group!.Select(e => e.Id)); // 组结构完整
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("c1")); // 子已加载
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("c2"));

        await host.ShutdownAsync();
    }

    // ── P0-3：结构步中途失败 → 已成功叶必须回滚（不留半应用态）──

    [Fact]
    public async Task Structural_step_partial_failure_rolls_back_all()
    {
        // ok 叶结构变（name 变）成功 + bad 叶结构变失败 → ok 必须回旧 name 运行
        await using var host = await StartAsync("""
            - id: ok
              name: ./ok
            - id: bad
              name: ./bad
            """);

        var error = await Assert.ThrowsAnyAsync<KeystoneException>(() => host.ApplyConfigAsync(EntryParser.Parse("""
            - id: ok
              name: ./ok-v2
            - id: bad
              name: ./bad-v2
            """)));

        // 失败后树回到旧态（ok 的 name 回滚）
        var okEntry = host.DumpConfig().Single(e => e.Id == "ok");
        Assert.Equal("./ok", okEntry.Name); // D-4/P0-3：树已复原

        await host.ShutdownAsync();
    }

    // ── D-5：Removed 回滚（失败后已删条目按原父/原下标重建并重载）──

    [Fact]
    public async Task Removed_entries_are_restored_on_failure()
    {
        await using var host = await StartAsync("""
            - id: victim
              name: ./victim
            - id: bad
              name: ./bad
            """);

        var error = await Assert.ThrowsAnyAsync<Exception>(() => host.ApplyConfigAsync(EntryParser.Parse("""
            - id: bad
              name: ./bad-v2
            """)));

        // D-5：victim 被删除后因 bad 失败回滚 → 重建并回到 Active
        Assert.Contains(host.DumpConfig(), e => e.Id == "victim");
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("victim"));

        await host.ShutdownAsync();
    }

    // ── D-4：失败复原运行时（UpdateEntryAsync 冷路径失败 → 插件用旧条目重启）──

    [Fact]
    public async Task UpdateEntry_failure_restores_plugin_runtime()
    {
        await using var host = await StartAsync("- id: a\n  name: ./a\n");

        // name 变（冷路径）+ 源损坏 → 失败；插件必须以旧 name 复原运行时（Active）
        var error = await Assert.ThrowsAnyAsync<Exception>(() =>
            host.UpdateEntryAsync("a", new EntryOptions { Id = "a", Name = "./nonexistent-src" }));

        Assert.Equal("./a", host.DumpConfig().Single(e => e.Id == "a").Name); // 树复原
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("a")); // D-4：运行时也复原

        await host.ShutdownAsync();
    }
}
