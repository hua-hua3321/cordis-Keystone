namespace Keystone.Hosting.Tests;

/// <summary>
/// DC-16（17-doc-compliance-audit / 08 §3）：disabled 挂起运行行为。
/// 修复前：disabled/isolate 字段有模型，运行行为未实现——disabled 条目照常加载。
/// 兑现：disabled=true 挂起不删（树保留、不加载；改回即恢复）；父组 disabled → 子树全部挂起。
/// </summary>
public class DisabledEntryTests
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
    public async Task Disabled_entry_is_suspended_not_loaded()
    {
        await using var host = new KeystoneHost(Options());

        await host.StartAsync("""
            - id: fs
              name: ./plugins/fs
              disabled: true
            """);

        var entry = host.DumpConfig().Single(e => e.Id == "fs");
        Assert.True(entry.Disabled); // 挂起不删：树保留
        Assert.Throws<Keystone.Core.Errors.KeystoneException>(() => host.GetPluginState("fs")); // 未加载
    }

    [Fact]
    public async Task Parent_group_disabled_suspends_whole_subtree()
    {
        // 08 §3：父组 disabled → 子树全部挂起（组自身永不被挂起）
        await using var host = new KeystoneHost(Options());

        await host.StartAsync("""
            - id: group1
              disabled: true
              group:
                - id: child-a
                  name: ./plugins/child-a
                - id: child-b
                  name: ./plugins/child-b
            - id: outside
              name: ./plugins/outside
            """);

        Assert.Throws<Keystone.Core.Errors.KeystoneException>(() => host.GetPluginState("child-a"));
        Assert.Throws<Keystone.Core.Errors.KeystoneException>(() => host.GetPluginState("child-b"));
        Assert.Equal(Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active, host.GetPluginState("outside")); // 组外不受影响
    }

    [Fact]
    public async Task Re_enabling_entry_loads_it()
    {
        // 改回即恢复（08 §3：依赖它的 PENDING 插件随之加载）
        await using var host = new KeystoneHost(Options());

        await host.StartAsync("- id: fs\n  name: ./plugins/fs\n  disabled: true\n");

        await host.SetEntryDisabledAsync("fs", disabled: false);

        Assert.Equal(Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active, host.GetPluginState("fs"));
        Assert.Null(host.DumpConfig().Single(e => e.Id == "fs").Disabled); // 条目恢复正常
    }

    [Fact]
    public async Task Disabling_active_entry_unloads_but_keeps_tree()
    {
        await using var host = new KeystoneHost(Options());

        await host.StartAsync("- id: fs\n  name: ./plugins/fs\n");
        Assert.Equal(Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active, host.GetPluginState("fs"));

        await host.SetEntryDisabledAsync("fs", disabled: true);

        Assert.True(host.DumpConfig().Single(e => e.Id == "fs").Disabled); // 树保留
        Assert.Throws<Keystone.Core.Errors.KeystoneException>(() => host.GetPluginState("fs")); // 已卸载
    }

    [Fact]
    public async Task Disabled_dependent_does_not_wait_for_dependencies()
    {
        // 挂起条目不参与门控拓扑（不 PENDING 占坑）
        await using var host = new KeystoneHost(new KeystoneHostOptions
        {
            ManifestProvider = e => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
                e.Id!, "1.0.0", "X.cs", ["cordis-runtime"], [], e.Id == "telemetry" ? ["fs"] : []),
            SourceProvider = e => new Keystone.Runtime.Plugins.Loading.PluginSource(
                e.Id!, e.Id == "fs" ? HostTestSources.ProviderSource : HostTestSources.DependentSource),
        });

        await host.StartAsync("""
            - id: telemetry
              name: ./plugins/telemetry
              inject: [fs]
              disabled: true
            """);

        // telemetry 挂起 → 不等待 fs（若参与拓扑会 PENDING 30s 超时）；树保留
        Assert.Throws<Keystone.Core.Errors.KeystoneException>(() => host.GetPluginState("telemetry"));
        var telemetry = host.DumpConfig().Single(e => e.Id == "telemetry");
        Assert.True(telemetry.Disabled);
    }
}
