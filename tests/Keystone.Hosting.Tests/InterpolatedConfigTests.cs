namespace Keystone.Hosting.Tests;

/// <summary>
/// DC-8 宿主接线（17-doc-compliance-audit）：StaticInterpolator 由 KeystoneHostOptions
/// 的 EnvProvider/FileProvider 注入，StartAsync 解析时展开（否则仍是"零调用"）。
/// </summary>
public class InterpolatedConfigTests
{
    private static KeystoneHostOptions Options(
        Func<string, string?>? env = null,
        Func<string, string?>? file = null)
        => new()
        {
            ManifestProvider = e => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
                e.Id!, "1.0.0", "X.cs", ["cordis-runtime"], [], []),
            SourceProvider = e => new Keystone.Runtime.Plugins.Loading.PluginSource(
                e.Id!, HostTestSources.DependentSource),
            EnvProvider = env,
            FileProvider = file,
        };

    [Fact]
    public async Task StartAsync_expands_env_tags_via_options()
    {
        await using var host = new KeystoneHost(Options(
            env: name => name == "PLUGIN_DATA_DIR" ? "/custom/data" : null));

        await host.StartAsync("""
            - id: fs
              name: ./plugins/fs
              config:
                root: !!env PLUGIN_DATA_DIR
            """);

        var config = (Dictionary<string, object?>)host.DumpConfig().Single(e => e.Id == "fs").Config!;
        Assert.Equal("/custom/data", config["root"]);
    }

    [Fact]
    public async Task StartAsync_without_providers_keeps_original_behavior()
    {
        await using var host = new KeystoneHost(Options());

        await host.StartAsync("""
            - id: fs
              name: ./plugins/fs
              config:
                root: !!env PLUGIN_DATA_DIR
            """);

        // 未配置提供者 → 不展开（原语义）
        var config = (Dictionary<string, object?>)host.DumpConfig().Single(e => e.Id == "fs").Config!;
        Assert.Equal("PLUGIN_DATA_DIR", config["root"]);
    }
}
