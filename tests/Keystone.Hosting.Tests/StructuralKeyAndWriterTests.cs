namespace Keystone.Hosting.Tests;

/// <summary>
/// P68（19 号审计 P2-5 + P2-27 + P2-21/LG-21）：
/// P2-5 结构键统一——UpdateEntryAsync 判定与 ConfigDiffer 同语义
///（name/inject/生效 isolate/形状/父；修复前宿主键缺 isolate → isolate 变更误走热路径）；
/// P2-27 ConfigUpdate 事件面对齐——纯内存模式（无 ConfigFilePath）CRUD 全触发
///（修复前 Create/Update 走 ScheduleWriteBack 早退不通知、Remove 触发——面不齐）；
/// P2-21 宿主自建 logger factory 的 Dispose 断言（RingBufferLogs.IsDisposed）。
/// </summary>
public class StructuralKeyAndWriterTests
{
    private static KeystoneHostOptions Options() => new()
    {
        ManifestProvider = e => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
            e.Id!, "1.0.0", "X.cs", ["cordis-runtime"], [], []),
        SourceProvider = e => new Keystone.Runtime.Plugins.Loading.PluginSource(
            e.Id!, HostTestSources.DependentSource),
    };

    // ── P2-5：isolate 变更走冷路径 ──

    [Fact]
    public async Task UpdateEntry_isolate_change_takes_cold_path()
    {
        await using var host = new KeystoneHost(Options());
        var reloads = 0;
        host.PluginReloading += (_, _) => reloads++;
        await host.StartAsync("""
            - id: a
              name: ./plugins/a
              isolate:
                fs: true
            """);

        await host.UpdateEntryAsync("a", new Keystone.Config.Entries.EntryOptions
        {
            Isolate = new Dictionary<string, Keystone.Config.Entries.IsolateSpec>
            {
                ["fs"] = Keystone.Config.Entries.IsolateSpec.Shared("label"), // isolate 变更
            },
        });

        Assert.True(reloads >= 1, "isolate 变更必须冷重启（修复前宿主结构键缺 isolate → 误走热路径）");
        await host.ShutdownAsync();
    }

    // ── P2-27：纯内存 CRUD 全通知 ──

    [Fact]
    public async Task Pure_memory_crud_fires_config_update_on_create_and_update()
    {
        await using var host = new KeystoneHost(Options()); // 无 ConfigFilePath = 纯内存
        await host.StartAsync("- id: a\n  name: ./plugins/a\n");

        var updates = 0;
        host.ConfigUpdate += (_, _) => updates++;

        var before = updates;
        await host.CreateEntryAsync(new Keystone.Config.Entries.EntryOptions
        {
            Id = "b",
            Name = "./plugins/b",
        });
        Assert.True(updates > before, "纯内存 Create 必须通知（修复前 ScheduleWriteBack 早退不通知）");

        before = updates;
        await host.UpdateEntryAsync("a", new Keystone.Config.Entries.EntryOptions
        {
            Config = new Dictionary<string, object?> { ["k"] = "v" },
        });
        Assert.True(updates > before, "纯内存 Update 必须通知（修复前早退不通知）");

        before = updates;
        await host.RemoveEntryAsync("b");
        Assert.True(updates > before, "纯内存 Remove 通知（现状已触发——面对齐回归）");
        await host.ShutdownAsync();
    }

    // ── P2-21/LG-21：自建 factory Dispose 断言 ──

    [Fact]
    public async Task Shutdown_disposes_owned_logger_factory()
    {
        var options = Options();
        options.ServiceOptions = new Dictionary<string, IReadOnlyDictionary<string, object?>>
        {
            ["logger"] = new Dictionary<string, object?> { ["capacity"] = 16 },
        };
        await using var host = new KeystoneHost(options);
        await host.StartAsync("- id: a\n  name: ./plugins/a\n");

        Assert.NotNull(host.RingBufferLogs);
        Assert.False(host.RingBufferLogs!.IsDisposed); // 关闭前在役

        await host.ShutdownAsync();
        Assert.True(host.RingBufferLogs.IsDisposed); // ShutdownAsync 经 owned factory Dispose 传导
    }
}
