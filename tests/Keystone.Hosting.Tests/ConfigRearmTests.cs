using Keystone.Config.Validation;
using Keystone.Core.Errors;
using Keystone.Runtime.Plugins.Lifecycle;

namespace Keystone.Hosting.Tests;

/// <summary>
/// P72-T1（对齐 Cordis fiber.update 非 ACTIVE 路径）：配置修复 re-arm。
/// Cordis 语义（fiber.ts:726-733）：update() 时 fiber 非 ACTIVE（含加载失败）→ 存新 config、
/// 清错误、重新激活——配置修复即自动重试。修复前 Keystone：从未托管成功的条目
///（启动期 config 校验失败进 _failedEntries / 编译失败未建 loader）收到新配置只改树不动运行时，
/// 插件死等到依赖变化或手动 reload。disabled 条目保持只改树（挂起语义不变）。
/// </summary>
public class ConfigRearmTests
{
    private const string OkSource = """
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        public sealed class OkPlugin : IPlugin
        {
            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
                => Task.CompletedTask;

            public Task DisposeAsync() => Task.CompletedTask;
        }
        """;

    private static KeystoneHostOptions Options() => new()
    {
        ManifestProvider = e => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
            e.Id!, "1.0.0", "X.cs", ["cordis-runtime"], [], []),
        SourceProvider = e => new Keystone.Runtime.Plugins.Loading.PluginSource(e.Id!, OkSource),
        ConfigSchemaProvider = e => string.Equals(e.Id, "p", StringComparison.Ordinal)
            ? new ConfigSchema([new ConfigField("mode", Required: true, Default: null)])
            : null,
    };

    [Fact]
    public async Task Config_fix_rearms_previously_failed_entry()
    {
        await using var host = new KeystoneHost(Options());
        // 初始 config 带未知字段 → schema 校验失败 → 条目 FAILED（隔离语义，不阻断启动）
        await host.StartAsync("- id: p\n  name: ./p\n  config:\n    other: 1\n");

        Assert.Equal(PluginLifecycleState.Failed, host.GetPluginState("p")); // 前置：确实失败

        // 配置修复（合法 config）→ 对齐 Cordis：非 ACTIVE fiber 收到 update 即重新激活
        await host.UpdatePluginAsync("p", new Dictionary<string, object?> { ["mode"] = "fast" }, save: false);

        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("p")); // 修复前：仍 Failed（只改树 no-op）
        await host.ShutdownAsync();
    }

    [Fact]
    public async Task Disabled_entry_config_change_stays_unloaded()
    {
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("- id: p\n  name: ./p\n  disabled: true\n");

        // disabled 挂起语义不受 re-arm 影响：配置变化仍只改树不加载
        await host.UpdatePluginAsync("p", new Dictionary<string, object?> { ["mode"] = "fast" }, save: false);

        Assert.Throws<KeystoneException>(() => host.GetPluginState("p")); // 未加载（非 Failed 非 Active）
        await host.ShutdownAsync();
    }
}
