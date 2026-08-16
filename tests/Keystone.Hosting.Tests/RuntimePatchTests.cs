using Keystone.Config.Entries;
using Keystone.Runtime.Plugins.Lifecycle;

namespace Keystone.Hosting.Tests;

/// <summary>
/// CA-5 宿主接线（P61）：KeystoneHostOptions.ConfigPatches → StartAsync 解析后、manifest 校验前应用
/// （patch 后的树才进校验——对齐 Cordis patch 在 schema 前生效）。
/// </summary>
public class RuntimePatchTests
{
    private static KeystoneHostOptions Options(IReadOnlyList<EntryPatch>? patches) => new()
    {
        ManifestProvider = e => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
            e.Id!, "1.0.0", "X.cs", ["cordis-runtime"], [], []),
        SourceProvider = e => new Keystone.Runtime.Plugins.Loading.PluginSource(
            e.Id!, HostTestSources.DependentSource),
        ConfigPatches = patches,
    };

    [Fact]
    public async Task ConfigPatches_apply_before_validation_and_load()
    {
        // patch 插入根 → 插入的条目参与 manifest 校验并被加载（对齐 Cordis patch 后启动）
        var patches = new List<EntryPatch>
        {
            new(GroupId: null, Insert: [new EntryOptions { Id = "patched", Name = "./plugins/patched" }], Overrides: null),
        };
        await using var host = new KeystoneHost(Options(patches));

        await host.StartAsync("- id: base\n  name: ./plugins/base\n");

        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("patched")); // 插入条目已加载
        Assert.Contains(host.DumpConfig(), e => e.Id == "patched"); // 树含插入条目

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task ConfigPatches_override_config_reaches_plugin()
    {
        // patch 覆盖 config → 插件收到 patch 后的值（读后覆盖生效）
        var patches = new List<EntryPatch>
        {
            new(GroupId: null, Insert: null,
                Overrides: new Dictionary<string, EntryOptions>
                {
                    ["base"] = new EntryOptions { Id = "base", Config = new Dictionary<string, object?> { ["v"] = 99 } },
                }),
        };
        await using var host = new KeystoneHost(Options(patches));

        await host.StartAsync("- id: base\n  name: ./plugins/base\n");

        var entry = Assert.Single(host.DumpConfig());
        var config = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(entry.Config);
        Assert.Equal(99, config["v"]); // patch 的 config 生效（patch 字典原值 int）

        await host.ShutdownAsync();
    }
}
