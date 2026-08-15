using Keystone.Runtime.Plugins.Loading;
using Keystone.Runtime.Plugins.Manifest;

namespace Keystone.Hosting.Tests;

/// <summary>
/// DC-19 宿主接线：IPluginSource 配置后插件源码经获取端抽象获取（优先于 SourceProvider 委托）。
/// </summary>
public class PluginSourceWiringTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("keystone-wire-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Host_loads_plugin_via_source_abstraction()
    {
        Directory.CreateDirectory(Path.Combine(_root, "fs"));
        await File.WriteAllTextAsync(Path.Combine(_root, "fs", "Plugin.cs"), HostTestSources.DependentSource);

        await using var host = new KeystoneHost(new KeystoneHostOptions
        {
            ManifestProvider = e => new PluginManifest(e.Id!, "1.0.0", "Plugin.cs", [], [], []),
            PluginSource = new LocalPluginSource(_root), // 获取端抽象（DC-19）
        });
        await host.StartAsync("- id: fs\n  name: ./fs\n");

        Assert.Equal(
            Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active,
            host.GetPluginState("fs"));
    }

    [Fact]
    public async Task Source_abstraction_takes_priority_over_delegate()
    {
        var delegateCalled = false;
        await using var host = new KeystoneHost(new KeystoneHostOptions
        {
            ManifestProvider = e => new PluginManifest(e.Id!, "1.0.0", "Plugin.cs", [], [], []),
            SourceProvider = _ =>
            {
                delegateCalled = true;
                return new Keystone.Runtime.Plugins.Loading.PluginSource("fs", HostTestSources.DependentSource);
            },
            PluginSource = new StaticSource(),
        });
        await host.StartAsync("- id: fs\n  name: ./fs\n");

        Assert.Equal(
            Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active,
            host.GetPluginState("fs"));
        Assert.False(delegateCalled); // 抽象优先——委托不调用
    }

    private sealed class StaticSource : IPluginSource
    {
        public Task<PluginSource> FetchAsync(PluginManifest manifest, CancellationToken cancellationToken = default)
            => Task.FromResult(new PluginSource(manifest.Id, HostTestSources.DependentSource));
    }
}
