using Keystone.Runtime.Plugins.Lifecycle;

namespace Keystone.Hosting.Tests;

/// <summary>
/// CA-2 插件源文件 watcher（18 §2 P2，P62）：ReloadPluginAsync 冷重启管线完备（重编译+换 ALC+quiesce），
/// 仅缺触发器。PluginFileWatcher（防抖合并）+ EnablePluginWatch()（与 EnableConfigWatch 对称，opt-in）。
/// </summary>
public class PluginFileWatchTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("keystone-pwatch-").FullName;

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

    private string PluginPath => Path.Combine(_directory, "watched.cs");

    // 每测唯一的插件类型名（跨 ALC 并行测试防撞名）
    private string TypeName { get; } = $"Watched{Guid.NewGuid():N}";

    private KeystoneHostOptions Options(string source) => new()
    {
        ManifestProvider = e => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
            e.Id!, "1.0.0", "watched.cs", ["cordis-runtime"], [], []),
        SourceProvider = e => new Keystone.Runtime.Plugins.Loading.PluginSource(e.Id!, source),
        PluginSource = new Keystone.Runtime.Plugins.Loading.LocalPluginSource(_directory),
    };

    private string SourceV1() => $$"""
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        public sealed class {{TypeName}} : IPlugin
        {
            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
            {
                context.Provide("pw-service", "v1");
                return Task.CompletedTask;
            }

            public Task DisposeAsync() => Task.CompletedTask;
        }
        """;

    private string SourceV2() => $$"""
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        public sealed class {{TypeName}} : IPlugin
        {
            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
            {
                context.Provide("pw-service", "v2-reloaded");
                return Task.CompletedTask;
            }

            public Task DisposeAsync() => Task.CompletedTask;
        }
        """;

    private static async Task WritePluginAsync(string path, string content)
    {
        File.Delete(path); // 先删再写确保触发事件
        await File.WriteAllTextAsync(path, content);
    }

    [Fact]
    public async Task Plugin_file_change_triggers_reload_with_new_assembly()
    {
        await WritePluginAsync(PluginPath, SourceV1());
        await using var host = new KeystoneHost(Options(SourceV1()));
        await host.StartAsync("- id: pw\n  name: ./plugins/pw\n");
        host.EnablePluginWatch(); // CA-2：插件源 watcher 接线（opt-in）

        var reloaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.PluginReloading += (_, e) =>
        {
            if (e.EntryId == "pw")
            {
                reloaded.TrySetResult();
            }
        };

        await WritePluginAsync(PluginPath, SourceV2()); // 源文件变更
        var timeout = Task.Delay(TimeSpan.FromSeconds(30));
        var completed = await Task.WhenAny(reloaded.Task, timeout);
        if (completed == timeout)
        {
            Assert.Fail("plugin watcher did not trigger reload within 30s");
        }

        // 冷重启凭证：重载后 GetPluginState 仍 Active（新 ALC 实例接管；编译失败会 FAILED）
        await Task.Delay(200); // 等 reload 收尾
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("pw")); // 热替换成功非失败

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task Unmatched_file_change_is_noop()
    {
        await WritePluginAsync(PluginPath, SourceV1());
        await using var host = new KeystoneHost(Options(SourceV1()));
        await host.StartAsync("- id: pw\n  name: ./plugins/pw\n");
        host.EnablePluginWatch();

        var reloading = false;
        host.PluginReloading += (_, _) => reloading = true;

        // 无匹配条目的文件变更（别的文件）→ 无操作
        await WritePluginAsync(Path.Combine(_directory, "other.cs"), "// unrelated");
        await Task.Delay(400); // 防抖窗口 + 余量

        Assert.False(reloading);
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("pw"));

        await host.ShutdownAsync();
    }
}
