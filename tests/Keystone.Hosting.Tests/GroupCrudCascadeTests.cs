using Keystone.Core.Errors;
using Keystone.Config.Entries;
using Keystone.Runtime.Plugins.Lifecycle;

namespace Keystone.Hosting.Tests;

/// <summary>
/// CA-10 组条目 CRUD 级联（18 §2 唯一 P0 正确性，P58）：
/// 修复前：RemoveEntryAsync(组id) 只从树删除组——整组插件孤儿续跑（仅 ApplyConfigAsync 路径被
/// ConfigDiffer 扁平化间接弥补，直接 API 调用必泄漏）；CreateEntryAsync 组条目只发 EntryInit 不加载
/// children——运行期建组 = 空壳。
/// 兑现：组删 = 逆序逐叶级联卸载（对齐 Cordis group remove 逐子卸载序）；组建 = 逐叶加载
/// （挂起继承 DC-16）；组移动 = 纯树操作（插件不迁——成员 context 链不变，与 Cordis 差异注明）。
/// </summary>
public class GroupCrudCascadeTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("keystone-group-crud-").FullName;

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

    private static void AssertNotLoaded(KeystoneHost host, string id)
    {
        var error = Assert.Throws<KeystoneException>(() => host.GetPluginState(id));
        Assert.Equal(ErrorCode.GatingServiceNotFound, error.Code); // 未加载（已卸载/从未加载）
    }

    [Fact]
    public async Task RemoveEntry_group_disposes_all_descendants_in_reverse_order()
    {
        // 孤儿泄漏修复核心：删组 → 组内全部叶子插件逐一卸载（逆序：后声明先卸，对齐 Cordis 卸载序）+ 树无残留
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("""
            - id: g
              name: ./plugins/g
              group:
                - id: a
                  name: ./plugins/a
                - id: b
                  name: ./plugins/b
                - id: h
                  name: ./plugins/h
                  group:
                    - id: c
                      name: ./plugins/c
            """);

        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("a"));
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("b"));
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("c"));

        var disposingOrder = new List<string>();
        host.EntryDisposing += (_, e) => disposingOrder.Add(e.EntryId);

        await host.RemoveEntryAsync("g");

        AssertNotLoaded(host, "a"); // 整组卸载——不再孤儿续跑
        AssertNotLoaded(host, "b");
        AssertNotLoaded(host, "c");
        Assert.Equal(["c", "b", "a"], disposingOrder); // 深度优先声明序的逆序（嵌套叶子最先）
        Assert.DoesNotContain(host.DumpConfig(), e => e.Id == "g"); // 树无残留

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task RemoveEntry_leaf_still_disposes_only_that_leaf()
    {
        // 既有语义回归保护：删叶子 = 只卸该叶（组内兄弟不受连坐）
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("""
            - id: g
              name: ./plugins/g
              group:
                - id: a
                  name: ./plugins/a
                - id: b
                  name: ./plugins/b
            """);

        await host.RemoveEntryAsync("a");

        AssertNotLoaded(host, "a");
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("b")); // 兄弟不受影响
        Assert.Contains(host.DumpConfig(), e => e.Id == "g"); // 组仍在（成员少一）

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task CreateEntry_with_group_loads_children()
    {
        // 空壳组修复核心：运行期建组（带子） → 子叶逐叶加载至 Active
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("");

        await host.CreateEntryAsync(new EntryOptions
        {
            Id = "g",
            Name = "./plugins/g",
            Group =
            [
                new EntryOptions { Id = "x", Name = "./plugins/x" },
                new EntryOptions { Id = "y", Name = "./plugins/y" },
            ],
        });

        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("x"));
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("y"));
        Assert.Contains(host.DumpConfig(), e => e.Id == "g");

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task CreateEntry_disabled_group_skips_children()
    {
        // 挂起继承（DC-16）：disabled 组建组 → 子树不加载（组条目自身永不加载）
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("");

        await host.CreateEntryAsync(new EntryOptions
        {
            Id = "g",
            Name = "./plugins/g",
            Disabled = true,
            Group = [new EntryOptions { Id = "x", Name = "./plugins/x" }],
        });

        AssertNotLoaded(host, "x"); // 挂起组的子叶不参与加载
        Assert.Contains(host.DumpConfig(), e => e.Id == "g"); // 树保留（挂起非删除）

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task MoveEntry_group_is_pure_tree_operation()
    {
        // 组移动 = 纯树操作：插件不重载不迁移（成员 context 链与 realm 谱系不变——与 Cordis 差异：
        // Cordis 组移动重挂 fiber；Keystone 组只承载声明谱系，运行时拓扑不受移动影响）
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("""
            - id: g
              name: ./plugins/g
              group:
                - id: a
                  name: ./plugins/a
            - id: target
              name: ./plugins/target
              group: []
            """);

        var reloads = 0;
        host.PluginReloading += (_, _) => reloads++;

        await host.MoveEntryAsync("g", "target");

        Assert.Equal(0, reloads); // 无重载
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("a")); // 成员保持运行
        var target = host.DumpConfig().Single(e => e.Id == "target");
        Assert.Contains(target.Group!, c => c.Id == "g"); // 树已移动

        await host.ShutdownAsync();
    }
}
