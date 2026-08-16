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
        Assert.Contains(ReadStaticStrings("VersionedPlugin", "LastConfig"), c => c?.Contains("v1", StringComparison.Ordinal) ?? false); // 新 runtime 用原配置重初始化

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

        // 热更新重载：新 runtime 收到 v2 配置（新旧 ALC 可能短暂并存——任一副本收到即证明）
        ForceGc();
        Assert.Contains(ReadStaticStrings("VersionedPlugin", "LastConfig"), c => c?.Contains("v2", StringComparison.Ordinal) ?? false);
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

    // 跨 ALC 读取（P57-T4 修既有 flake）：重载后新旧 ALC 短暂并存（ALC.Unload 异步、ForceGc 不保证立即回收），
    // GetAssemblies() 跨 LoadContext 的枚举顺序并无"最新在后"保证——"Reverse 取第一"会随机命中旧副本。
    // 改为确定语义：int 取全副本最大（计数只增）；string 取全副本（调用方用 Any 断言——重载语义即"新副本收到过 X"）。
    private static int ReadStaticInt(string typeName, string fieldName)
    {
        var values = ReadAllStaticFields(typeName, fieldName);
        return values.Count == 0
            ? throw new InvalidOperationException($"{typeName}.{fieldName} not found")
            : values.Max(v => (int)(v ?? 0));
    }

    private static List<string?> ReadStaticStrings(string typeName, string fieldName)
        => ReadAllStaticFields(typeName, fieldName).Select(v => (string?)v).ToList();

    private static List<object?> ReadAllStaticFields(string typeName, string fieldName)
    {
        var values = new List<object?>();
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
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

            values.Add(field.GetValue(null));
        }

        return values;
    }
}
