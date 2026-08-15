using Keystone.Config.Entries;
using Keystone.Runtime.Plugins.Loading;
using Keystone.Runtime.Plugins.Manifest;

namespace Keystone.Hosting.Tests;

public static class HostTestSources
{
    public const string ProviderSource = """
        using System;
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        public sealed class ProviderPlugin : IPlugin
        {
            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
            {
                context.Provide("fs", new object());
                return Task.CompletedTask;
            }

            public Task DisposeAsync() => Task.CompletedTask;
        }
        """;

    public const string DependentSource = """
        using System;
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        public sealed class DependentPlugin : IPlugin
        {
            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
                => Task.CompletedTask;

            public Task DisposeAsync() => Task.CompletedTask;
        }
        """;
}

public class KeystoneHostTests
{
    private static readonly PluginManifest ProviderManifest =
        new("fs", "1.0.0", "Provider.cs", ["cordis-runtime"], ["fs"], []);

    private static readonly PluginManifest DependentManifest =
        new("telemetry", "1.0.0", "Dependent.cs", ["cordis-runtime"], [], ["fs"]);

    private static KeystoneHostOptions Options()
        => new()
        {
            ManifestProvider = e => e.Id == "fs" ? ProviderManifest : DependentManifest,
            SourceProvider = e => e.Id == "fs"
                ? new PluginSource(e.Id!, HostTestSources.ProviderSource)
                : new PluginSource(e.Id!, HostTestSources.DependentSource),
        };

    [Fact]
    public async Task Start_activates_plugins_with_dependency_gating_and_shutdown_quiesces()
    {
        await using var host = new KeystoneHost(Options());

        await host.StartAsync("""
            - id: fs
              name: ./plugins/fs
            - id: telemetry
              name: ./plugins/telemetry
              inject: [fs]
            """);

        // 依赖门控：telemetry 依赖 fs，拓扑加载后两者均 ACTIVE
        Assert.Equal(Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active, host.GetPluginState("fs"));
        Assert.Equal(Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active, host.GetPluginState("telemetry"));

        await host.ShutdownAsync();
        await host.ShutdownAsync(); // 幂等
    }

    [Fact]
    public async Task CreateEntry_loads_new_plugin_and_remove_unloads()
    {
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("- id: fs\n  name: ./plugins/fs\n");
        Assert.Equal(Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active, host.GetPluginState("fs"));

        var id = await host.CreateEntryAsync(new EntryOptions { Id = "telemetry", Name = "./plugins/telemetry" });

        Assert.Equal("telemetry", id);
        Assert.Equal(Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active, host.GetPluginState("telemetry"));

        await host.RemoveEntryAsync("telemetry");

        Assert.Throws<Keystone.Core.Errors.KeystoneException>(() => host.GetPluginState("telemetry"));
    }

    [Fact]
    public async Task MoveEntry_failure_rolls_back_position()
    {
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("- id: fs\n  name: ./plugins/fs\n- id: group1\n  group: []\n");
        var before = host.DumpConfig().Single(e => e.Id == "fs");

        // 移动到不存在的组 → 抛，位置不变（F5 移动失败回滚）
        await Assert.ThrowsAsync<Keystone.Core.Errors.KeystoneException>(() => host.MoveEntryAsync("fs", "nonexistent"));

        var after = host.DumpConfig().Single(e => e.Id == "fs");
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task ResolveEntry_handles_nested_ids()
    {
        await using var host = new KeystoneHost(new KeystoneHostOptions
        {
            ManifestProvider = e => new PluginManifest(e.Id!, "1.0.0", "X.cs", ["cordis-runtime"], [], []),
            SourceProvider = e => new PluginSource(e.Id!, HostTestSources.DependentSource),
        });
        await host.StartAsync("""
            - id: group1
              group:
                - id: child
                  name: ./plugins/child
            """);

        var resolved = host.ResolveEntry("group1:child");

        Assert.Equal("child", resolved.Id);
    }

    [Fact]
    public async Task MountAsync_programmatic_mount_runs_and_unloads()
    {
        // H2 端到端：编程式挂载 → 门控 → 运行 → 卸载
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("");

        await host.MountAsync(new PluginSource("fs", HostTestSources.ProviderSource), ProviderManifest);

        Assert.Equal(Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active, host.GetPluginState("fs"));

        await host.RemoveEntryAsync("fs");
        Assert.Throws<Keystone.Core.Errors.KeystoneException>(() => host.GetPluginState("fs"));
    }

    [Fact]
    public async Task EntryInit_event_raised_on_entry_creation()
    {
        await using var host = new KeystoneHost(Options());
        var initiated = new List<string>();
        host.EntryInit += (_, args) => initiated.Add(args.Entry.Id!);

        await host.StartAsync("- id: fs\n  name: ./plugins/fs\n");
        await host.CreateEntryAsync(new EntryOptions { Id = "telemetry", Name = "./plugins/telemetry" });

        Assert.Contains("fs", initiated);
        Assert.Contains("telemetry", initiated);
    }

    [Fact]
    public async Task PatchContext_waterfall_can_veto()
    {
        await using var host = new KeystoneHost(Options());
        var applied = false;
        host.SubscribePatchContext((_, next) => Task.CompletedTask); // 不调 next：否决
        host.SubscribePatchContext(async (_, next) => await next());

        await host.PatchContextAsync(new EntryOptions { Id = "x" }, () =>
        {
            applied = true;
            return Task.CompletedTask;
        });

        Assert.False(applied, "PatchContext waterfall 否决后 apply 不执行");
    }
}
