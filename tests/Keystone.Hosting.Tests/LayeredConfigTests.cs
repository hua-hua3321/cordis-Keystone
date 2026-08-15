namespace Keystone.Hosting.Tests;

/// <summary>
/// DC-7（17-doc-compliance-audit / 08 §4）：宿主接分层叠加。
/// 修复前：EntryTree.ApplyLayers 孤立工具类，宿主只吃单 YAML 字符串。
/// 兑现：StartAsync(IEnumerable&lt;string&gt;) 多层按序叠加（base → profile → patch → overlay）——
/// patch 按 id 合并（提供的字段覆盖，未提供保留）；显式 insert 插入；层内重复 id fail-fast。
/// 每层独立解析（含 DC-8 插值），叠加以条目 id 为主键。
/// </summary>
public class LayeredConfigTests
{
    private static KeystoneHostOptions Options()
        => new()
        {
            ManifestProvider = e => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
                e.Id!, "1.0.0", "X.cs", ["cordis-runtime"], [], []),
            SourceProvider = e => new Keystone.Runtime.Plugins.Loading.PluginSource(
                e.Id!, HostTestSources.DependentSource),
        };

    [Fact]
    public async Task StartAsync_applies_layers_in_order()
    {
        await using var host = new KeystoneHost(Options());

        await host.StartAsync(
        [
            """
            - id: fs
              name: ./plugins/fs
              config:
                root: /data
            - id: telemetry
              name: ./plugins/telemetry
            """,
            // patch 层：按 id 合并（config 覆盖，name 未提供 → 保留 base）
            """
            - id: fs
              config:
                root: /new-data
            """,
        ]);

        var merged = host.DumpConfig();
        var fs = merged.Single(e => e.Id == "fs");
        var telemetry = merged.Single(e => e.Id == "telemetry");

        var config = (Dictionary<string, object?>)fs.Config!;
        Assert.Equal("/new-data", config["root"]); // patch 覆盖
        Assert.Equal("./plugins/fs", fs.Name); // 未提供字段保留 base
        Assert.NotNull(telemetry); // base 其余条目不受影响

        Assert.Equal(Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active, host.GetPluginState("fs"));
    }

    [Fact]
    public async Task Patch_layer_can_insert_new_entries()
    {
        await using var host = new KeystoneHost(Options());

        await host.StartAsync(
        [
            "- id: fs\n  name: ./plugins/fs\n",
            "- id: extra\n  name: ./plugins/extra\n  insert: true\n",
        ]);

        var merged = host.DumpConfig();
        Assert.Equal(2, merged.Count);
        Assert.Contains(merged, e => e.Id == "extra");
        Assert.Equal(Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active, host.GetPluginState("extra"));
    }

    [Fact]
    public async Task Duplicate_id_within_layer_fails_fast()
    {
        await using var host = new KeystoneHost(Options());

        // 层内重复 id = 配置错误（启动期 fail-fast，08 §4）
        await Assert.ThrowsAsync<Keystone.Core.Errors.KeystoneException>(() => host.StartAsync(
        [
            "- id: fs\n  name: ./plugins/fs\n",
            """
            - id: dup
              name: ./a
            - id: dup
              name: ./b
            """,
        ]));
    }

    [Fact]
    public async Task Layers_are_interpolated_before_merging()
    {
        // DC-8 × DC-7 组合：每层独立解析（含 !!env 展开），展开后再叠加
        var options = Options();
        options.EnvProvider = name => name == "ROOT" ? "/env-data" : null;

        await using var host = new KeystoneHost(options);
        await host.StartAsync(
        [
            "- id: fs\n  name: ./plugins/fs\n  config:\n    root: !!env ROOT\n",
            "- id: fs\n  insert: false\n",
        ]);

        var fs = host.DumpConfig().Single(e => e.Id == "fs");
        var config = (Dictionary<string, object?>)fs.Config!;
        Assert.Equal("/env-data", config["root"]); // base 层插值展开（patch 未提供 config → 保留 base 已展开值）
    }

    [Fact]
    public async Task Single_layer_overload_keeps_original_behavior()
    {
        // 兼容：StartAsync(string) 单层 = 原语义
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("- id: fs\n  name: ./plugins/fs\n");

        Assert.Single(host.DumpConfig());
        Assert.Equal(Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active, host.GetPluginState("fs"));
    }
}
