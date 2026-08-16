namespace Keystone.Hosting.Tests;

/// <summary>
/// P71-T2（硬编码审计批）：文件监听防抖窗口入 WatcherOptions（默认 100ms 保持不变）。
/// 修复前：ConfigFileWatcher/PluginFileWatcher 各自 private static readonly Debounce = 100ms——
/// 编辑器连发保存 vs 热重载响应延迟的权衡点不可调。
/// </summary>
public class WatcherOptionsWiringTests
{
    private static KeystoneHostOptions Options() => new()
    {
        ManifestProvider = e => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
            e.Id!, "1.0.0", "X.cs", ["cordis-runtime"], [], []),
        SourceProvider = e => new Keystone.Runtime.Plugins.Loading.PluginSource(
            e.Id!, "public sealed class X { }"),
    };

    [Fact]
    public async Task Config_watch_debounce_flows_from_options()
    {
        var dir = Directory.CreateTempSubdirectory("keystone-watch-");
        var configPath = Path.Combine(dir.FullName, "cfg.yaml");
        await File.WriteAllTextAsync(configPath, string.Empty);
        var options = Options();
        options.ConfigFilePath = configPath;
        options.Watchers.ConfigFileDebounce = TimeSpan.FromMilliseconds(123);
        await using var host = new KeystoneHost(options);
        await host.StartAsync(string.Empty); // 空树（无插件条目）

        host.EnableConfigWatch();

        Assert.Equal(TimeSpan.FromMilliseconds(123), host.ConfigWatcherDebounce);
        await host.ShutdownAsync();
    }

    [Fact]
    public async Task Plugin_watch_debounce_flows_from_options()
    {
        var dir = Directory.CreateTempSubdirectory("keystone-pwatch-");
        var options = Options();
        options.PluginSource = new Keystone.Runtime.Plugins.Loading.LocalPluginSource(dir.FullName);
        options.Watchers.PluginFileDebounce = TimeSpan.FromMilliseconds(234);
        await using var host = new KeystoneHost(options);
        await host.StartAsync(string.Empty);

        host.EnablePluginWatch();

        Assert.Equal(TimeSpan.FromMilliseconds(234), host.PluginWatcherDebounce);
        await host.ShutdownAsync();
    }
}
