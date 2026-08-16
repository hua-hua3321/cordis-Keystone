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

        // 冷重启凭证：重载后 GetPluginState 仍 Active（新 ALC 实例接管；编译失败会 FAILED）。
        // 轮询等待 reload 收尾（固定 200ms 在全量并行负载下不稳——防抖+编译耗时随机器负载波动）
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (true)
        {
            try
            {
                if (host.GetPluginState("pw") == PluginLifecycleState.Active)
                {
                    break; // 新 ALC 实例接管完成
                }
            }
            catch (Keystone.Core.Errors.KeystoneException)
            {
                // reload 瞬态窗口：旧实例已卸、新实例未挂（DC-6 先卸后建）
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                Assert.Fail("plugin not Active within 10s after reload");
            }

            await Task.Delay(50);
        }

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
