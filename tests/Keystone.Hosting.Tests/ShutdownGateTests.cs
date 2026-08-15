using Keystone.Runtime.Plugins.Loading;
using Keystone.Runtime.Plugins.Manifest;

namespace Keystone.Hosting.Tests;

/// <summary>
/// DC-3 测试（17-doc-compliance-audit，09 §4 全局 quiesce）：
/// 关闭时入口拒绝新任务 + 幂等 + 未收敛插件审计 + 停止能力域监督。
/// </summary>
public class ShutdownGateTests
{
    private static KeystoneHostOptions Options() => new()
    {
        ManifestProvider = _ => new PluginManifest("p", "1.0.0", "P.cs", ["cordis-runtime"], [], []),
        SourceProvider = _ => new PluginSource("p", ConfigInjectionTests.ConfigAwareSource),
    };

    [Fact]
    public async Task Shutdown_rejects_new_entries()
    {
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("- id: p\n  name: ./plugins/p\n");

        await host.ShutdownAsync();

        // 关闭后：新入口直接拒绝（09 §4 第 1 步）
        await Assert.ThrowsAsync<Keystone.Core.Errors.KeystoneException>(
            () => host.CreateEntryAsync(new Keystone.Config.Entries.EntryOptions { Id = "new", Name = "./plugins/p" }));
    }

    [Fact]
    public async Task Shutdown_is_idempotent()
    {
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("- id: p\n  name: ./plugins/p\n");

        await host.ShutdownAsync();
        await host.ShutdownAsync(); // 幂等：二次调用直接返回

        Assert.Empty(host.UncollectedPlugins); // 正常关闭：无未收敛
    }

    [Fact]
    public async Task Shutdown_with_timeout_records_uncollected_plugins()
    {
        // 插件 DisposeAsync 卡死 → 关闭超时强制退出 + 记录未收敛（09 §4 第 6 步）
        var host = new KeystoneHost(new KeystoneHostOptions
        {
            ManifestProvider = _ => new PluginManifest("hung", "1.0.0", "H.cs", ["cordis-runtime"], [], []),
            SourceProvider = _ => new PluginSource("hung", """
                using System;
                using System.Threading.Tasks;
                using System.Collections.Generic;
                using Keystone.Runtime.Context;
                using Keystone.Runtime.Plugins.Lifecycle;

                public sealed class HungPlugin : IPlugin
                {
                    public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
                        => Task.CompletedTask;

                    public Task DisposeAsync() => Task.Delay(TimeSpan.FromSeconds(60)); // 卡死
                }
                """),
            ShutdownTimeout = TimeSpan.FromMilliseconds(200), // 短超时强制退出
        });
        await host.StartAsync("- id: hung\n  name: ./plugins/hung\n");

        await host.ShutdownAsync(); // 200ms 后强制退出

        Assert.Contains("hung", host.UncollectedPlugins); // 未收敛审计
    }
}
