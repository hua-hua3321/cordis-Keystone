using Keystone.Runtime.Plugins.Loading;
using Keystone.Runtime.Plugins.Manifest;

namespace Keystone.Runtime.Tests;

/// <summary>
/// DC-19（17-doc-compliance-audit / ADR-0001 决策 1-2）：IPluginSource/IPluginHost 抽象边界。
/// 修复前：无接口，SourceProvider 委托替代——获取端无法替换演进（远程分发/进程隔离无扩展点）。
/// 兑现：IPluginSource = 获取端抽象（只替换获取，不动编译/ALC/dispose 管线）；
/// LocalPluginSource = 本地文件初始实现（manifest.Main 相对根目录解析）；
/// IPluginHost = 运行形态扩展点（预留，本期仅同进程 ALC 描述符）。
/// </summary>
public class PluginSourceAbstractionTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("keystone-src-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }

        GC.SuppressFinalize(this);
    }

    private static PluginManifest Manifest(string main = "Plugin.cs")
        => new("p1", "1.0.0", main, ["cordis-runtime"], [], []);

    [Fact]
    public async Task LocalSource_fetches_file_relative_to_root()
    {
        var main = Path.Combine(_root, "Plugin.cs");
        await File.WriteAllTextAsync(main, "/* plugin code */");

        var source = new LocalPluginSource(_root);
        var fetched = await source.FetchAsync(Manifest());

        Assert.Equal("p1", fetched.Id);
        Assert.Equal("/* plugin code */", fetched.Code);
    }

    [Fact]
    public async Task Missing_file_fails_with_config_error()
    {
        var source = new LocalPluginSource(_root);

        var exception = await Assert.ThrowsAsync<Keystone.Core.Errors.KeystoneException>(
            () => source.FetchAsync(Manifest("Gone.cs")));

        Assert.Equal(Keystone.Core.Errors.ErrorCode.ConfigProviderFailed, exception.Code);
        Assert.Contains("Gone.cs", exception.Message);
    }

    [Fact]
    public async Task Search_falls_back_to_manifest_id_directory()
    {
        // 约定目录布局：{root}/{id}/{main}（多插件仓库形态）
        var nested = Path.Combine(_root, "p1", "Plugin.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(nested)!);
        await File.WriteAllTextAsync(nested, "/* nested */");

        var source = new LocalPluginSource(_root);
        var fetched = await source.FetchAsync(Manifest());

        Assert.Equal("/* nested */", fetched.Code);
    }

    [Fact]
    public void DefaultHost_describes_same_process_alc_model()
    {
        var host = DefaultPluginHost.Instance;

        // 接口面经 IPluginHost 暴露（扩展点预留）；本期唯一形态 = 同进程 ALC（ADR-0001 决策 1）
        Assert.Equal("same-process-alc", ((IPluginHost)host).IsolationModel);
    }
}
