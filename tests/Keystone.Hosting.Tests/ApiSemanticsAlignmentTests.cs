using Keystone.Config.Entries;

namespace Keystone.Hosting.Tests;

/// <summary>
/// P67（19 号审计 D-2/D-3 + P2-6/7/8）：宿主 API 语义对齐 Cordis。
/// D-2 UpdateEntryAsync 逐字段合并（entry.ts:146-154 patch 语义——提供覆盖/缺省保留，
/// 修复前整条目替换：未传字段被清空）；
/// D-3 parent 缺省 = 不动（tree.ts:114-124——修复前缺省 = 移根）；显式 "" = 根；
/// P2-6 ResolveEntry 任意深度 `:` 嵌套（tree.ts:76-87——修复前仅两级）；
/// P2-7 无 id 条目 ensureId 自动分配（修复前分层丢弃 + diff ToDictionary(null) 崩）；
/// P2-8 MoveEntryAsync 失败回插精确原位（修复前回滚到根——与报错矛盾）。
/// </summary>
public class ApiSemanticsAlignmentTests
{
    private static KeystoneHostOptions Options() => new()
    {
        ManifestProvider = e => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
            e.Id!, "1.0.0", "X.cs", ["cordis-runtime"], [], []),
        SourceProvider = e => new Keystone.Runtime.Plugins.Loading.PluginSource(
            e.Id!, HostTestSources.DependentSource),
    };

    // ── D-2：字段合并 ──

    [Fact]
    public async Task UpdateEntry_merges_fields_unprovided_keep_current()
    {
        await using var host = new KeystoneHost(Options());
        var reloads = 0;
        host.PluginReloading += (_, _) => reloads++;
        await host.StartAsync("""
            - id: a
              name: ./plugins/a
              inject: [fs]
              config:
                mode: v1
            """);

        // 只提供 config（其余字段缺省）——修复前 Name/Inject 被清空 + 结构键变 → 冷重启
        await host.UpdateEntryAsync("a", new EntryOptions
        {
            Config = new Dictionary<string, object?> { ["mode"] = "v2" },
        });

        var resolved = host.ResolveEntry("a");
        Assert.Equal("./plugins/a", resolved.Name); // 保留（D-2）
        Assert.Contains("fs", resolved.Inject); // 保留（D-2）
        Assert.Equal("v2", ((Dictionary<string, object?>)resolved.Config!)["mode"]); // 提供的覆盖
        Assert.Equal(0, reloads); // 结构键不变 → 热路径（无冷重启）
        await host.ShutdownAsync();
    }

    // ── D-3：parent 缺省不动 / 显式根 ──

    [Fact]
    public async Task UpdateEntry_parent_default_keeps_current_group()
    {
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("""
            - id: g
              name: ./plugins/g
              group:
                - id: a
                  name: ./plugins/a
            - id: b
              name: ./plugins/b
            """);

        await host.UpdateEntryAsync("a", new EntryOptions
        {
            Config = new Dictionary<string, object?> { ["k"] = "v" },
        }); // 不带 parent——修复前被挪根

        var root = host.DumpConfig();
        Assert.Contains(root.Single(e => e.Id == "g").Group!, c => c.Id == "a"); // 仍在组内（D-3）
        await host.ShutdownAsync();
    }

    [Fact]
    public async Task UpdateEntry_explicit_root_sentinel_moves_to_root()
    {
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("""
            - id: g
              name: ./plugins/g
              group:
                - id: a
                  name: ./plugins/a
            """);

        await host.UpdateEntryAsync("a", new EntryOptions
        {
            Name = "./plugins/a",
        }, parent: KeystoneHost.RootParent); // 显式 "" = 根

        Assert.Contains(host.DumpConfig(), e => e.Id == "a"); // 已在根
        Assert.DoesNotContain(host.DumpConfig().Single(e => e.Id == "g").Group ?? [], c => c.Id == "a");
        await host.ShutdownAsync();
    }

    // ── P2-6：任意深度嵌套解析 ──

    [Fact]
    public async Task ResolveEntry_walks_arbitrary_depth()
    {
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("""
            - id: outer
              name: ./plugins/outer
              group:
                - id: mid
                  name: ./plugins/mid
                  group:
                    - id: leaf
                      name: ./plugins/leaf
            """);

        Assert.Equal("leaf", host.ResolveEntry("outer:mid:leaf").Id); // 修复前两级限制 → 抛 not found
        await host.ShutdownAsync();
    }

    // ── P2-7：无 id 条目自动分配 ──

    [Fact]
    public async Task No_id_entries_get_generated_ids()
    {
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("""
            - name: ./plugins/a
            - name: ./plugins/b
            """);

        var ids = host.DumpConfig().Select(e => e.Id).ToList();
        Assert.All(ids, id => Assert.False(string.IsNullOrWhiteSpace(id))); // ensureId（修复前丢弃/崩）
        Assert.Equal(2, ids.Distinct(StringComparer.Ordinal).Count()); // 唯一
        await host.ShutdownAsync();
    }

    // ── P2-8：MoveEntry 失败回插精确原位 ──

    [Fact]
    public async Task MoveEntry_failure_restores_exact_original_position()
    {
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("""
            - id: g
              name: ./plugins/g
              group:
                - id: first
                  name: ./plugins/first
                - id: second
                  name: ./plugins/second
            - id: leafparent
              name: ./plugins/leafparent
            """);

        // 目标"父"是叶（存在但非组）→ InsertEntry 抛 → 回滚
        await Assert.ThrowsAsync<Keystone.Core.Errors.KeystoneException>(
            () => host.MoveEntryAsync("second", "leafparent"));

        var g = host.DumpConfig().Single(e => e.Id == "g");
        Assert.Equal(["first", "second"], (g.Group ?? []).Select(c => c.Id)); // 原组原下标（修复前回滚到根）
        await host.ShutdownAsync();
    }
}
