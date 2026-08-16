using Keystone.Config.Entries;
using Keystone.Core.Errors;
using Keystone.Runtime.Plugins.Lifecycle;

namespace Keystone.Hosting.Tests;

/// <summary>
/// CA-6 initial 引导接线（18 §2 P1，P60）：EnsureInitialAsync 一直是死代码（宿主零调用、
/// KeystoneHostOptions 无 initial 选项）——首启"文件不存在则写入初始配置"（对齐 Cordis
/// include Service.init ENOENT+initial 先写再读）。
/// 兑现：InitialEntries 选项 + StartFromFileAsync()（文件入口）：无文件+initial → 写入再启动；
/// 文件已存在 → initial 忽略；无文件无 initial → 明确报错。
/// </summary>
public class InitialBootstrapTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("keystone-initial-").FullName;
    private string ConfigPath => Path.Combine(_directory, "config.yaml");

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }

        GC.SuppressFinalize(this);
    }

    private KeystoneHostOptions Options(EntryOptions? initial = null, string? configPath = null)
    {
        var options = new KeystoneHostOptions
        {
            ManifestProvider = e => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
                e.Id!, "1.0.0", "X.cs", ["cordis-runtime"], [], []),
            SourceProvider = e => new Keystone.Runtime.Plugins.Loading.PluginSource(
                e.Id!, HostTestSources.DependentSource),
            ConfigFilePath = configPath ?? ConfigPath,
        };
        if (initial is not null)
        {
            options.InitialEntries = [initial];
        }

        return options;
    }

    [Fact]
    public async Task No_file_with_initial_writes_then_starts()
    {
        // 首启：文件不存在 + initial → 写入初始配置再启动（文件落地 + 插件 Active）
        Assert.False(File.Exists(ConfigPath));
        await using var host = new KeystoneHost(Options(new EntryOptions { Id = "boot", Name = "./plugins/boot" }));

        await host.StartFromFileAsync();

        Assert.True(File.Exists(ConfigPath)); // initial 已写盘
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("boot"));
        Assert.Contains("boot", File.ReadAllText(ConfigPath)); // 内容含初始条目

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task Existing_file_ignores_initial()
    {
        // 文件已存在 → initial 不覆盖（现网配置优先，对齐 Cordis include 语义）
        await File.WriteAllTextAsync(ConfigPath, "- id: real\n  name: ./plugins/real\n");
        await using var host = new KeystoneHost(Options(new EntryOptions { Id = "boot", Name = "./plugins/boot" }));

        await host.StartFromFileAsync();

        Assert.DoesNotContain("boot", File.ReadAllText(ConfigPath)); // 未被 initial 覆盖
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("real")); // 启动的是文件内容
        Assert.Throws<KeystoneException>(() => host.GetPluginState("boot"));

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task No_file_without_initial_fails_clearly()
    {
        // 无文件无 initial → 明确报错（对齐 include ENOENT 报错语义）
        await using var host = new KeystoneHost(Options());

        var error = await Assert.ThrowsAsync<KeystoneException>(() => host.StartFromFileAsync());
        Assert.Equal(ErrorCode.ConfigValidationFailed, error.Code);

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task StartFromFile_without_config_path_fails()
    {
        // 未配置 ConfigFilePath → 明确报错（无文件入口可读）
        await using var host = new KeystoneHost(Options(initial: null, configPath: "/definitely/not/used"));

        var error = await Assert.ThrowsAsync<KeystoneException>(() => host.StartFromFileAsync());
        Assert.Equal(ErrorCode.ConfigValidationFailed, error.Code);

        await host.ShutdownAsync();
    }
}
