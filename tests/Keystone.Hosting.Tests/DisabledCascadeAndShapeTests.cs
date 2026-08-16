using Keystone.Config.Entries;
using Keystone.Runtime.Plugins.Lifecycle;

namespace Keystone.Hosting.Tests;

/// <summary>
/// P65（19 号审计 P1-6/LD-13 + P1-7/LD-17）：disabled 级联 + 形状/归属结构键。
/// P1-6 修复前：运行期组翻转只动组条目，子叶照跑；disabled 组内叶单独 re-enable 绕过祖先直载。
/// P1-7 修复前：结构键不含 Group 形状/归属——叶↔组转换漏检（组子撞"not a group"）、跨组移动被丢弃。
/// </summary>
public class DisabledCascadeAndShapeTests
{
    private static KeystoneHostOptions Options() => new()
    {
        ManifestProvider = e => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
            e.Id!, "1.0.0", "X.cs", ["cordis-runtime"], [], []),
        SourceProvider = e => new Keystone.Runtime.Plugins.Loading.PluginSource(
            e.Id!, HostTestSources.DependentSource),
    };

    private static async Task<KeystoneHost> StartAsync(string yaml)
    {
        var host = new KeystoneHost(Options());
        await host.StartAsync(yaml);
        return host;
    }

    [Fact]
    public async Task Group_disable_runtime_flip_cascades_to_children()
    {
        // P1-6：boot 期祖先剪枝已对齐；运行期翻转必须级联（对齐 entry.ts:88-98 + group.ts:108-112）
        await using var host = await StartAsync("""
            - id: g
              name: ./g
              group:
                - id: c1
                  name: ./c1
                - id: c2
                  name: ./c2
            """);
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("c1"));

        await host.SetEntryDisabledAsync("g", true); // 运行期组翻转

        Assert.Contains(host.DumpConfig().Single(e => e.Id == "g").Group!, e => e.Id == "c1"); // 树保留
        Assert.Throws<Keystone.Core.Errors.KeystoneException>(() => host.GetPluginState("c1")); // 子叶级联卸载
        Assert.Throws<Keystone.Core.Errors.KeystoneException>(() => host.GetPluginState("c2"));

        await host.SetEntryDisabledAsync("g", false); // 恢复

        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("c1")); // 子叶级联重载
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("c2"));

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task Re_enable_inside_disabled_group_does_not_load()
    {
        // P1-6 旁路：disabled 组内叶子单独 re-enable → 祖先仍挂起 → 不得直载（ancestor 检查）
        await using var host = await StartAsync("""
            - id: g
              name: ./g
              disabled: true
              group:
                - id: c1
                  name: ./c1
                  disabled: true
            """);

        await host.SetEntryDisabledAsync("c1", false); // 叶子恢复，但组仍挂起

        Assert.Throws<Keystone.Core.Errors.KeystoneException>(() => host.GetPluginState("c1")); // 祖先挡住

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task Leaf_to_group_conversion_loads_children()
    {
        // P1-7：叶→组转换必须检出（组形状入结构键）+ 子进组（组加载管线）
        await using var host = await StartAsync("- id: a\n  name: ./a\n");

        await host.ApplyConfigAsync(EntryParser.Parse("""
            - id: a
              name: ./a
              group:
                - id: b
                  name: ./b
            """));

        var a = host.DumpConfig().Single(e => e.Id == "a");
        Assert.NotNull(a.Group); // 已是组
        Assert.Equal("b", a.Group!.Single().Id); // 子在组内
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("b")); // 子已加载

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task Diff_applies_move_between_groups()
    {
        // P1-7：跨组移动必须检出（归属入结构键，对齐 entry.ts:194 group 变=冷重启）
        await using var host = await StartAsync("""
            - id: g1
              name: ./g1
              group:
                - id: c
                  name: ./c
            - id: g2
              name: ./g2
              group: []
            """);

        await host.ApplyConfigAsync(EntryParser.Parse("""
            - id: g1
              name: ./g1
              group: []
            - id: g2
              name: ./g2
              group:
                - id: c
                  name: ./c
            """));

        Assert.Equal("c", host.DumpConfig().Single(e => e.Id == "g2").Group!.Single().Id); // 已移入 g2
        Assert.Empty(host.DumpConfig().Single(e => e.Id == "g1").Group!); // g1 空
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("c")); // 移动后仍 active

        await host.ShutdownAsync();
    }
}
