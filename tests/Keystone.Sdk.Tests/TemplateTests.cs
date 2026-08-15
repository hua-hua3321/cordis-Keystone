using System.Diagnostics;
using Keystone.Runtime.Plugins.Lifecycle;
using Keystone.Runtime.Plugins.Loading;
using Keystone.Runtime.Plugins.Manifest;
using Keystone.Runtime.Plugins.Services;

namespace Keystone.Sdk.Tests;

/// <summary>
/// 模板全链路（P11 验收 1）：dotnet new 创建 → 编译 → 挂载 → 运行 → 卸载。
/// </summary>
public class TemplateTests
{
    [Fact]
    public async Task Template_creates_plugin_that_compiles_loads_runs_and_unloads()
    {
        var templatePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "templates", "keystone-plugin"));
        Assert.True(Directory.Exists(templatePath), $"template not found: {templatePath}");

        var tmp = Directory.CreateTempSubdirectory("keystone-tpl-");
        try
        {
            RunDotNet(["new", "install", templatePath], tmp.FullName);
            var outDir = Path.Combine(tmp.FullName, "MyPlugin");
            RunDotNet(["new", "keystone-plugin", "-o", outDir, "-n", "MyPlugin"], tmp.FullName);

            var sourceFile = Path.Combine(outDir, "MyPlugin.cs");
            Assert.True(File.Exists(sourceFile), "dotnet new 应生成插件源文件");
            var source = await File.ReadAllTextAsync(sourceFile);

            // 编译 → ALC 加载 → 实例化 → 运行 → 卸载
            var manifest = new PluginManifest("myplugin", "1.0.0", "MyPlugin.cs", ["cordis-runtime"], [], []);
            await using var loader = await PluginLoader.CreateAsync(
                new PluginSource("myplugin", source),
                manifest,
                new ServiceRegistry(),
                id => new Runtime.Context.ContextFacade(id));

            Assert.Equal(PluginLifecycleState.Active, loader.Runtime.State);

            await loader.DisposeAsync();
            Assert.Equal(PluginLifecycleState.Disposed, loader.Runtime.State);
        }
        finally
        {
            try
            {
                Directory.Delete(tmp.FullName, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static void RunDotNet(IReadOnlyList<string> args, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        // 模板引擎根指向工作区（避免写 ~/.templateengine 的宿主环境限制）
        startInfo.Environment["DOTNET_CLI_HOME"] = Path.Combine(workingDirectory, ".dotnet-cli-home");
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("failed to start dotnet");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(TimeSpan.FromSeconds(60));

        Assert.True(process.ExitCode == 0, $"dotnet {string.Join(' ', args)} failed:\n{stdout}\n{stderr}");
    }
}
