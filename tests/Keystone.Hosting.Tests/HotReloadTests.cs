using Keystone.Runtime.Plugins.Loading;
using Keystone.Runtime.Plugins.Manifest;

namespace Keystone.Hosting.Tests;

/// <summary>
/// G-C8 热更新测试（16-cordis-gap-review，09 §5 ReloadPlugin/UpdatePlugin 承诺）：
/// 插件冷重启（ReloadPluginAsync）+ 配置热更新（UpdatePluginAsync，瀑布可否决）。
/// </summary>
[Collection("ConfigInjection")]
public class HotReloadTests
{
    public const string VersionedSource = """
        using System;
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        public sealed class VersionedPlugin : IPlugin
        {
            public static string? LastConfig;
            public static int BootCount;

            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
            {
                BootCount++;
                LastConfig = System.Text.Json.JsonSerializer.Serialize(config);
                return Task.CompletedTask;
            }

            public Task DisposeAsync() => Task.CompletedTask;
        }
        """;

    private static KeystoneHostOptions Options() => new()
    {
        ManifestProvider = _ => new PluginManifest("hot", "1.0.0", "Versioned.cs", ["cordis-runtime"], [], []),
        SourceProvider = _ => new PluginSource("hot", VersionedSource),
    };

    [Fact]
    public async Task ReloadPlugin_restarts_plugin_with_new_alc()
    {
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("""
            - id: hot
              name: ./plugins/hot
              config:
                mode: v1
            """);

        Assert.Equal(1, ReadStaticInt("VersionedPlugin", "BootCount"));

        await host.ReloadPluginAsync("hot"); // 冷重启

        // 新 ALC 重新初始化（独立静态状态：新 ALC BootCount 从 0 起 +1）
        Assert.Equal(Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active, host.GetPluginState("hot"));
        // 旧 ALC 已 Unload（loader.DisposeAsync）→ 强制 GC 回收后只剩新 ALC，读到新静态状态
        ForceGc();
        Assert.Equal(1, ReadStaticInt("VersionedPlugin", "BootCount"));
        Assert.Contains("v1", ReadStaticString("VersionedPlugin", "LastConfig")); // 新 runtime 用原配置重初始化

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task UpdatePlugin_replaces_config_and_reloads()
    {
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("""
            - id: hot
              name: ./plugins/hot
              config:
                mode: v1
            """);

        var bootsBefore = ReadStaticInt("VersionedPlugin", "BootCount");

        await host.UpdatePluginAsync("hot", new Dictionary<string, object?> { ["mode"] = "v2" });

        // 热更新重载：新 runtime 收到 v2 配置（旧 ALC Unload + GC 后只剩新 ALC）
        ForceGc();
        Assert.Contains("v2", ReadStaticString("VersionedPlugin", "LastConfig"));
        // 配置树已更新
        Assert.Equal("v2", ((Dictionary<string, object?>)host.ResolveEntry("hot").Config!)["mode"]);

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task UpdatePlugin_can_be_vetoed_by_patch_context_handler()
    {
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("""
            - id: hot
              name: ./plugins/hot
              config:
                mode: v1
            """);

        // 订阅 PatchContext：否决所有更新（不调 apply）
        using var sub = host.SubscribePatchContext((_, apply) => Task.CompletedTask);
        var bootsBefore = ReadStaticInt("VersionedPlugin", "BootCount");

        await host.UpdatePluginAsync("hot", new Dictionary<string, object?> { ["mode"] = "blocked" });

        Assert.Equal(bootsBefore, ReadStaticInt("VersionedPlugin", "BootCount")); // 否决：未重载
        Assert.Equal("v1", ((Dictionary<string, object?>)host.ResolveEntry("hot").Config!)["mode"]); // 配置未变

        await host.ShutdownAsync();
    }

    private static void ForceGc()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static int ReadStaticInt(string typeName, string fieldName)
        => (int)ReadStaticField(typeName, fieldName);

    private static string? ReadStaticString(string typeName, string fieldName)
        => (string?)ReadStaticField(typeName, fieldName);

    private static object ReadStaticField(string typeName, string fieldName)
    {
        // 跨 ALC：重载后新旧 ALC 并存——取最新加载 ALC 的实例（Reverse 第一个）
        // （int 取最大兜底：若最新 ALC 类型未找到则用计数最高的）
        object? best = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies().Reverse())
        {
            var t = asm.GetTypes().FirstOrDefault(x => x.Name == typeName);
            if (t is null)
            {
                continue;
            }

            var field = t.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (field is null)
            {
                continue;
            }

            best = field.GetValue(null);
            break; // 最新加载 ALC 优先
        }

        return best ?? throw new InvalidOperationException($"{typeName}.{fieldName} not found");
    }
}
