using Keystone.Config.Entries;
using Keystone.Runtime.Plugins.Lifecycle;

namespace Keystone.Hosting.Tests;

/// <summary>
/// CA-3 组级事务（18 §2 P1，P59）：组内逐条应用（声明序）+ 失败聚合 + 逆序回滚。
/// P2-31/LD-5（P68 注释修正）：应用为**串行**（非 group.ts:71 allSettled 并行）——
/// 逆序 undo 登记要求确定的失败前缀序；单错抛因/多错聚合面与 Cordis 等价，时序刻意差异（undo 确定性）。
/// 修复前：顺序应用、首错中断、无回滚——组更新一半失败 → 半应用状态（部分新叶已加载却上抛调用方，
/// 调用方重试/放弃均基于不一致树）。
/// 兑现（对齐 Cordis group 事务的失败面语义——应用序为声明序串行，见上注）：
/// 单错抛原因；多错 AggregateException；回滚 = 逆序撤销本次已成功变更（Added→Remove、ConfigChanged→Update 旧值）；
/// 回滚失败聚合进同一异常上抛。
/// </summary>
public class GroupTransactionTests
{
    private static readonly IReadOnlyDictionary<string, string> Sources = new Dictionary<string, string>
    {
        ["ok1"] = HostTestSources.DependentSource,
        ["ok2"] = HostTestSources.DependentSource,
        ["bad"] = "public class {", // 编译失败 → 加载失败
        ["bad2"] = "public class {",
    };

    private static KeystoneHostOptions Options() => new()
    {
        ManifestProvider = e => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
            e.Id!, "1.0.0", "X.cs", ["cordis-runtime"], [], []),
        SourceProvider = e => new Keystone.Runtime.Plugins.Loading.PluginSource(
            e.Id!, Sources.GetValueOrDefault(e.Id!, HostTestSources.DependentSource)),
    };

    [Fact]
    public async Task Group_apply_failure_rolls_back_added_siblings()
    {
        // 组内 2 新增(ok1/ok2) + 1 失败(bad) → 上抛单因；已成功的新增全部回滚（树复原 + 插件卸载）
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("""
            - id: g
              name: ./plugins/g
              group: []
            """);

        await Assert.ThrowsAsync<Keystone.Core.Errors.KeystoneException>(() => host.ApplyConfigAsync(EntryParser.Parse("""
            - id: g
              name: ./plugins/g
              group:
                - id: ok1
                  name: ./plugins/ok1
                - id: ok2
                  name: ./plugins/ok2
                - id: bad
                  name: ./plugins/bad
            """)));

        Assert.Equal([], host.DumpConfig().Single(e => e.Id == "g").Group!); // 树复原（新叶全撤）
        Assert.Throws<Keystone.Core.Errors.KeystoneException>(() => host.GetPluginState("ok1")); // 插件不在托管
        Assert.Throws<Keystone.Core.Errors.KeystoneException>(() => host.GetPluginState("ok2"));

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task Multiple_failures_aggregate()
    {
        // 双失败 → AggregateException 含 2 内因（单错抛原因的对照）
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("""
            - id: g
              name: ./plugins/g
              group: []
            """);

        var error = await Assert.ThrowsAsync<AggregateException>(() => host.ApplyConfigAsync(EntryParser.Parse("""
            - id: g
              name: ./plugins/g
              group:
                - id: bad
                  name: ./plugins/bad
                - id: ok1
                  name: ./plugins/ok1
                - id: bad2
                  name: ./plugins/bad2
            """)));

        Assert.Equal(2, error.InnerExceptions.Count); // 双因聚合（bad + bad2 两个编译失败）

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task Group_apply_success_loads_all_children()
    {
        // 正向路径回归保护：组更新全成功 → 全部 Active + 树生效
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("""
            - id: g
              name: ./plugins/g
              group: []
            """);

        await host.ApplyConfigAsync(EntryParser.Parse("""
            - id: g
              name: ./plugins/g
              group:
                - id: ok1
                  name: ./plugins/ok1
                - id: ok2
                  name: ./plugins/ok2
            """));

        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("ok1"));
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("ok2"));

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task Group_apply_rolls_back_config_changes()
    {
        // ConfigChanged 路径回滚：组内 1 config 更新成功 + 1 新增失败 → config 更新也撤销（旧值恢复）
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("""
            - id: g
              name: ./plugins/g
              group:
                - id: ok1
                  name: ./plugins/ok1
                  config:
                    mode: v1
            """);

        await Assert.ThrowsAsync<Keystone.Core.Errors.KeystoneException>(() => host.ApplyConfigAsync(EntryParser.Parse("""
            - id: g
              name: ./plugins/g
              group:
                - id: ok1
                  name: ./plugins/ok1
                  config:
                    mode: v2
                - id: bad
                  name: ./plugins/bad
            """)));

        var restored = host.DumpConfig().SelectMany(e => e.Group ?? []).Single(c => c.Id == "ok1");
        Assert.Equal("v1", ((Dictionary<string, object?>)restored.Config!)["mode"]); // 旧 config 恢复

        await host.ShutdownAsync();
    }
}
