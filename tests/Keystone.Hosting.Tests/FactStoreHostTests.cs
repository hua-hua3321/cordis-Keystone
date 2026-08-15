using Keystone.Runtime.Persistence;

namespace Keystone.Hosting.Tests;

/// <summary>
/// DC-11 宿主接线（17-doc-compliance-audit）：KeystoneHostOptions.EventStore →
/// 根 context 总线携带事实存储——插件生命周期事实经共享总线持久化（ADR-0009/03 §4）。
/// </summary>
public class FactStoreHostTests
{
    private const string FailingSource = """
        using System;
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        public sealed class FailingPlugin : IPlugin
        {
            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
                => throw new InvalidOperationException("boom");

            public Task DisposeAsync() => Task.CompletedTask;
        }
        """;

    [Fact]
    public async Task Host_persists_plugin_lifecycle_facts_via_shared_root_bus()
    {
        var store = new InMemoryEventStore();
        var host = new KeystoneHost(new KeystoneHostOptions
        {
            ManifestProvider = e => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
                e.Id!, "1.0.0", "X.cs", ["cordis-runtime"], [], []),
            SourceProvider = e => new Keystone.Runtime.Plugins.Loading.PluginSource(e.Id!, FailingSource),
            EventStore = store,
        });

        await host.StartAsync("- id: boom\n  name: ./plugins/boom\n");
        await host.ShutdownAsync();

        var names = new List<string>();
        await foreach (var fact in store.ReplayAsync(new ReplayQuery(), CancellationToken.None))
        {
            names.Add(fact.EventName ?? string.Empty);
        }

        Assert.Contains("PluginFailedFact", names); // 初始化失败事实已持久化
    }
}
