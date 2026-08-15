using Keystone.Config.Validation;
using Keystone.Runtime.Plugins.Loading;
using Keystone.Runtime.Plugins.Manifest;

namespace Keystone.Hosting.Tests;

/// <summary>
/// G-C1 配置注入测试（16-cordis-gap-review）：插件 InitializeAsync 应收到
/// schema 校验后、默认值补齐的完整配置（对齐 Cordis resolveConfig，fiber.ts:641）。
/// </summary>
[Collection("ConfigInjection")]
public class ConfigInjectionTests
{
    public const string ConfigAwareSource = """
        using System;
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        public sealed class ConfigAwarePlugin : IPlugin
        {
            public static IReadOnlyDictionary<string, object?>? Received;
            public static string? ConfigJson;

            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
            {
                Received = config;
                ConfigJson = System.Text.Json.JsonSerializer.Serialize(config);
                return Task.CompletedTask;
            }

            public Task DisposeAsync() => Task.CompletedTask;
        }
        """;

    private static readonly PluginManifest ConfigManifest =
        new("configurable", "1.0.0", "ConfigAware.cs", ["cordis-runtime"], [], []);

    private static readonly ConfigSchema TestSchema = new(
    [
        new ConfigField("root", Required: true, Default: null),
        new ConfigField("mode", Required: false, Default: "read-only"),
    ]);

    private static KeystoneHostOptions Options(ConfigSchema? schema = null) => new()
    {
        ManifestProvider = _ => ConfigManifest,
        SourceProvider = _ => new PluginSource("configurable", ConfigAwareSource),
        ConfigSchemaProvider = schema is null ? _ => null : _ => schema,
    };

    [Fact]
    public async Task Plugin_receives_validated_config_with_defaults_applied()
    {
        await using var host = new KeystoneHost(Options(TestSchema));

        await host.StartAsync("""
            - id: configurable
              name: ./plugins/configurable
              config:
                root: /data
            """);

        Assert.Equal(Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active, host.GetPluginState("configurable"));
        // 默认值补齐：mode 未提供 → "read-only"；root 原样
        Assert.Contains("read-only", ReadStaticString("ConfigAwarePlugin", "ConfigJson"));
        Assert.Contains("/data", ReadStaticString("ConfigAwarePlugin", "ConfigJson"));

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task Missing_required_field_fails_fast()
    {
        await using var host = new KeystoneHost(Options(TestSchema));

        // root 必填但缺失 → schema 校验失败 → 插件 FAILED（fail-fast，对齐 Cordis）
        await host.StartAsync("""
            - id: configurable
              name: ./plugins/configurable
              config:
                mode: write
            """);

        Assert.Equal(Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Failed, host.GetPluginState("configurable"));

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task Plugin_without_schema_receives_raw_config()
    {
        // 无 schema 声明 → 不校验，原始 config 直传（未接 schema 的插件不受影响）
        await using var host = new KeystoneHost(Options(schema: null));

        await host.StartAsync("""
            - id: configurable
              name: ./plugins/configurable
              config:
                custom: 42
            """);

        Assert.Equal(Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active, host.GetPluginState("configurable"));
        Assert.Contains("42", ReadStaticString("ConfigAwarePlugin", "ConfigJson"));

        await host.ShutdownAsync();
    }

    private static string? ReadStaticString(string typeName, string fieldName)
    {
        // 取最后加载的程序集（最新 ALC 的类型持有最新静态值）
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

            return (string?)field.GetValue(null);
        }

        return null;
    }
}
