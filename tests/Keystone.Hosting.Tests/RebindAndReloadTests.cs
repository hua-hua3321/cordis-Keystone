using Keystone.Runtime.Plugins.Loading;
using Keystone.Runtime.Plugins.Manifest;

namespace Keystone.Hosting.Tests;

/// <summary>
/// DC-6 测试（17-doc-compliance-audit，02 §3 rebind + ADR-0007）：
/// 同 scope 重复注册必须报错；热重载后服务注册保持可用（不误删/不冲突）。
/// </summary>
public class RebindAndReloadTests
{
    [Fact]
    public async Task Reload_plugin_keeps_service_available()
    {
        // 热重载：旧实例卸载（Unregister）→ 新实例启动（Register 同名服务）——不冲突、不丢失。
        // 验证：热重载后依赖方（inject fs）仍能 ACTIVE（服务注册保持）。
        var providerSource = """
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
        var dependentSource = """
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
        await using var host = new KeystoneHost(new KeystoneHostOptions
        {
            ManifestProvider = e => e.Id switch
            {
                "fs" => new PluginManifest("fs", "1.0.0", "P.cs", ["cordis-runtime"], ["fs"], []),
                "dep" => new PluginManifest("dep", "1.0.0", "D.cs", ["cordis-runtime"], [], ["fs"]),
                _ => throw new InvalidOperationException(),
            },
            SourceProvider = e => e.Id switch
            {
                "fs" => new PluginSource("fs", providerSource),
                "dep" => new PluginSource("dep", dependentSource),
                _ => throw new InvalidOperationException(),
            },
        });
        await host.StartAsync("""
            - id: fs
              name: ./plugins/fs
            - id: dep
              name: ./plugins/dep
              inject: [fs]
            """);

        Assert.Equal(Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active, host.GetPluginState("dep")); // 依赖已注入

        await host.ReloadPluginAsync("fs"); // 热重载（DC-6 顺序修复：先卸载再启动）

        Assert.Equal(Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active, host.GetPluginState("fs")); // 重载后 ACTIVE（服务注册成功）
        Assert.Equal(Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active, host.GetPluginState("dep")); // 依赖方保持

        await host.ShutdownAsync();
    }
}
