using Keystone.Config.Entries;

namespace Keystone.Hosting.Tests;

/// <summary>
/// DC-15（17-doc-compliance-audit / 09 §5 / 08 §6.3）：CRUD 落盘写回 + position 参数。
/// 修复前：_tree 纯内存（CRUD 不落盘）；CreateEntry/MoveEntry 无 position。
/// 兑现：CRUD 变更经 ConfigFileWriter 防抖写回（原子写 + 重试）；position 指定插入位置；
/// FlushConfigAsync 冲刷队列；Shutdown 排空写队列。
/// </summary>
public class CrudPersistenceTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("keystone-crud-").FullName;

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

    private KeystoneHostOptions Options(string? configPath = null)
    {
        var options = new KeystoneHostOptions
        {
            ManifestProvider = e => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
                e.Id!, "1.0.0", "X.cs", ["cordis-runtime"], [], []),
            SourceProvider = e => new Keystone.Runtime.Plugins.Loading.PluginSource(
                e.Id!, HostTestSources.DependentSource),
        };
        if (configPath is not null)
        {
            options.ConfigFilePath = configPath;
        }

        return options;
    }

    [Fact]
    public async Task CreateEntry_with_position_inserts_at_index()
    {
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("");

        await host.CreateEntryAsync(new EntryOptions { Id = "a", Name = "./a" });
        await host.CreateEntryAsync(new EntryOptions { Id = "b", Name = "./b" });
        await host.CreateEntryAsync(new EntryOptions { Id = "c", Name = "./c" }, position: 0);

        Assert.Equal(["c", "a", "b"], host.DumpConfig().Select(e => e.Id)); // 插入首位
    }

    [Fact]
    public async Task MoveEntry_with_position_reorders()
    {
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("- id: a\n  name: ./a\n- id: b\n  name: ./b\n- id: c\n  name: ./c\n");

        await host.MoveEntryAsync("c", newParent: null, position: 0);

        Assert.Equal(["c", "a", "b"], host.DumpConfig().Select(e => e.Id));
    }

    [Fact]
    public async Task Crud_changes_write_back_to_config_file()
    {
        var path = Path.Combine(_directory, "cordis.yml");
        await using var host = new KeystoneHost(Options(path));
        await host.StartAsync("- id: base\n  name: ./base\n");

        await host.CreateEntryAsync(new EntryOptions { Id = "added", Name = "./added" });
        await host.FlushConfigAsync(); // 冲刷防抖队列（08 §6.3 写防抖）

        Assert.True(File.Exists(path));
        var reloaded = Keystone.Config.Entries.EntryParser.Parse(await File.ReadAllTextAsync(path));
        Assert.Equal(["base", "added"], reloaded.Select(e => e.Id)); // 落盘 = 当前树
    }

    [Fact]
    public async Task Shutdown_drains_pending_write_back()
    {
        var path = Path.Combine(_directory, "cordis.yml");
        var host = new KeystoneHost(Options(path));
        await host.StartAsync("- id: base\n  name: ./base\n");
        await host.CreateEntryAsync(new EntryOptions { Id = "late", Name = "./late" });

        await host.ShutdownAsync(); // 不手动 Flush——关闭应排空写队列

        var reloaded = Keystone.Config.Entries.EntryParser.Parse(await File.ReadAllTextAsync(path));
        Assert.Contains(reloaded, e => e.Id == "late");
    }

    [Fact]
    public async Task Remove_persists_deletion()
    {
        var path = Path.Combine(_directory, "cordis.yml");
        await using var host = new KeystoneHost(Options(path));
        await host.StartAsync("- id: a\n  name: ./a\n- id: b\n  name: ./b\n");

        await host.RemoveEntryAsync("a");
        await host.FlushConfigAsync();

        var reloaded = Keystone.Config.Entries.EntryParser.Parse(await File.ReadAllTextAsync(path));
        Assert.Equal(["b"], reloaded.Select(e => e.Id));
    }

    [Fact]
    public async Task Without_config_path_no_file_written()
    {
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("");

        await host.CreateEntryAsync(new EntryOptions { Id = "x", Name = "./x" });
        await host.FlushConfigAsync();

        Assert.False(File.Exists(Path.Combine(_directory, "cordis.yml"))); // 未配置路径 → 纯内存
    }
}
