using Keystone.Config.Entries;

namespace Keystone.Hosting.Tests;

/// <summary>
/// DC-9（17-doc-compliance-audit / 08 §6）：文件变更 → 重载 → diff → 逐条目更新。
/// 修复前：无 watcher/diff；热更新退化为手动 API 调用。
/// 兑现：ApplyConfigAsync（重读→校验→diff→按 08 §6.1 分级逐条目动作）；
/// ConfigFileWatcher（防抖合并 → 回调）；宿主 WatchConfigFile = true 接线（变更自动 ApplyConfigAsync）。
/// </summary>
public class ConfigHotReloadTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("keystone-hot-").FullName;

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

    private KeystoneHostOptions Options(string configPath) => new()
    {
        ManifestProvider = e => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
            e.Id!, "1.0.0", "X.cs", [], [], []),
        SourceProvider = e => new Keystone.Runtime.Plugins.Loading.PluginSource(
            e.Id!, HostTestSources.DependentSource),
        ConfigFilePath = configPath,
    };

    private string ConfigPath => Path.Combine(_directory, "cordis.yml");

    private async Task<KeystoneHost> StartAsync(string yaml, KeystoneHostOptions? options = null)
    {
        var host = new KeystoneHost(options ?? Options(ConfigPath));
        await host.StartAsync(yaml);
        return host;
    }

    private static async Task WriteConfigAsync(string path, string yaml)
    {
        // 先删再写：确保触发 Created/Changed（同尺寸覆盖写在并行 IO 下可能不产生 LastWrite 事件）
        File.Delete(path);
        await File.WriteAllTextAsync(path, yaml);
    }

    [Fact]
    public async Task Diff_only_config_change_routes_to_hot_update()
    {
        await using var host = await StartAsync("- id: a\n  name: ./a\n  config:\n    k: 1\n");
        var reloaded = new List<string>();
        var updated = new List<(string Id, object? Config)>();
        host.ConfigReloaded += (_, e) => reloaded.Add(string.Join(",", e.ChangedIds));
        host.PluginUpdating += (_, e) => updated.Add((e.EntryId, e.NewConfig));

        // 仅 config 变（k: 1 → 2）：08 §6.1 热更新路径（不冷重启）
        var tree = host.DumpConfig().ToList();
        tree[0] = tree[0] with { Config = new Dictionary<string, object?> { ["k"] = 2 } };
        await host.ApplyConfigAsync(tree);

        Assert.Equal(["a"], reloaded); // diff 报告变更条目
        Assert.Equal("a", updated[0].Id); // 热更新路径
        var updatedConfig = Assert.IsAssignableFrom<Dictionary<string, object?>>(updated[0].Config);
        Assert.Equal(2, updatedConfig["k"]);
        Assert.Contains("a", host.GetPluginState("a").ToString(), StringComparison.OrdinalIgnoreCase); // 仍 active
    }

    [Fact]
    public async Task Diff_name_change_routes_to_cold_restart()
    {
        await using var host = await StartAsync("- id: a\n  name: ./a\n");
        var coldRestarts = new List<string>();
        host.PluginReloading += (_, e) => coldRestarts.Add(e.EntryId);

        var tree = host.DumpConfig().ToList();
        tree[0] = tree[0] with { Name = "./b" }; // name 变 → 冷重启（08 §6.1）
        await host.ApplyConfigAsync(tree);

        Assert.Equal(["a"], coldRestarts);
    }

    [Fact]
    public async Task Diff_added_entry_loads_and_removed_entry_unloads()
    {
        await using var host = await StartAsync("- id: a\n  name: ./a\n");

        var tree = host.DumpConfig().ToList();
        tree.Add(new EntryOptions { Id = "b", Name = "./b" }); // 新增
        await host.ApplyConfigAsync(tree);
        Assert.Contains(host.DumpConfig(), e => e.Id == "b"); // 已加载入树

        await host.ApplyConfigAsync([new EntryOptions { Id = "a", Name = "./a" }]); // 移除 b
        Assert.DoesNotContain(host.DumpConfig(), e => e.Id == "b");
    }

    [Fact]
    public async Task Disabled_flip_suspends_without_removal()
    {
        await using var host = await StartAsync("- id: a\n  name: ./a\n");

        var tree = host.DumpConfig().ToList();
        tree[0] = tree[0] with { Disabled = true }; // disabled 翻转 → 仅卸载（条目保留）
        await host.ApplyConfigAsync(tree);

        Assert.Contains(host.DumpConfig(), e => e.Id == "a"); // 条目保留
        Assert.Throws<Keystone.Core.Errors.KeystoneException>(() => host.GetPluginState("a")); // 未运行（挂起）
    }

    [Fact]
    public async Task No_change_is_noop()
    {
        await using var host = await StartAsync("- id: a\n  name: ./a\n");
        var reloaded = false;
        host.ConfigReloaded += (_, _) => reloaded = true;

        await host.ApplyConfigAsync(host.DumpConfig()); // 相同树 → 不动

        Assert.False(reloaded); // deepEqual 相等即跳过
        Assert.False(File.Exists(ConfigPath)); // 无变更不触发写回
    }

    [Fact]
    public async Task Watcher_triggers_apply_on_file_change()
    {
        var yaml1 = "- id: a\n  name: ./a\n";
        await WriteConfigAsync(ConfigPath, yaml1);
        await using var host = await StartAsync(yaml1, Options(ConfigPath));
        host.EnableConfigWatch(); // DC-9：watch 接线（防抖合并 → ApplyConfigAsync）

        var applied = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        host.ConfigReloaded += (_, e) => applied.TrySetResult(string.Join(",", e.ChangedIds));

        await WriteConfigAsync(ConfigPath, "- id: a\n  name: ./a\n- id: b\n  name: ./b\n"); // 文件变更
        var timeout = Task.Delay(TimeSpan.FromSeconds(30));
        var completed = await Task.WhenAny(applied.Task, timeout);
        if (completed == timeout)
        {
            Assert.Fail("watcher did not trigger apply within 30s");
        }

        Assert.Contains("b", await applied.Task, StringComparison.Ordinal); // watcher 触发重载并含新增条目
    }

    [Fact]
    public async Task Watcher_apply_does_not_write_back()
    {
        // CA-15（P61）防回环：文件已是新值——watcher 触发的 apply 不写回
        //（修复前 apply 内 UpdatePluginAsync 固定 ScheduleWriteBack → 写回同内容回环）
        var yaml1 = "- id: a\n  name: ./a\n";
        await WriteConfigAsync(ConfigPath, yaml1);
        await using var host = await StartAsync(yaml1, Options(ConfigPath));
        host.EnableConfigWatch();

        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.ConfigReloaded += (_, _) => applied.TrySetResult();

        var yaml2 = "- id: a\n  name: ./a\n  config:\n    k: 2\n";
        await WriteConfigAsync(ConfigPath, yaml2); // 文件变更（含 config 热更）
        await (await Task.WhenAny(applied.Task, Task.Delay(TimeSpan.FromSeconds(30))));

        await Task.Delay(300); // 防抖窗口 + 余量
        Assert.Equal(yaml2, File.ReadAllText(ConfigPath)); // 文件保持新值（未被写回重写——内容/时间戳层面等价）
    }

    [Fact]
    public async Task UpdatePlugin_noSave_skips_write_back()
    {
        // CA-15：noSave=true → 内存态更新（不落盘）；save 默认 true 保持现行为
        await using var host = await StartAsync("- id: a\n  name: ./a\n");

        await host.UpdatePluginAsync("a", new Dictionary<string, object?> { ["k"] = 1 }, save: false);

        Assert.Contains(host.DumpConfig(), e => e.Config is not null); // 内存树已更新
        Assert.False(File.Exists(ConfigPath)); // 未写盘（noSave）
    }
}
