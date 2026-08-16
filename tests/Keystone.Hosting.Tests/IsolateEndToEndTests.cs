using Keystone.Runtime.Plugins.Lifecycle;
using Keystone.Runtime.Plugins.Manifest;

namespace Keystone.Hosting.Tests;

/// <summary>
/// CA-1 宿主端到端（18 §2 第 3 步配置接线，P57-T5）：
/// 三 context 工厂按 entry.Isolate 算 realm——组谱系 #声明处Id / @label / 默认 ""；
/// 门控域 == 解析域（PluginRuntime 与 context 工厂同 map，对齐 Cordis reflect.provide/notify/resolve 同键路由）；
/// F10：isolate 变更（含组级声明）→ 生效 realm 变 → 受影响条目冷重启。
/// 证明手法：不同 realm 放可区分值，按消费者解析结果断言路由（比"门控挂起"更强）。
/// 宿主加载语义 = 激活或失败（LoadSourceAsync await 终态）→ 测试保证各门控在其域内加载时即可满足。
/// 类型名全套件唯一（跨 ALC 反射读取确定性，P57-T4 教训）。
/// </summary>
public class IsolateEndToEndTests
{
    private const string ProviderTemplate = """
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        public sealed class __TYPE__ : IPlugin
        {
            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
            {
                context.Provide("fs", "__VALUE__");
                return Task.CompletedTask;
            }

            public Task DisposeAsync() => Task.CompletedTask;
        }
        """;

    private const string ConsumerTemplate = """
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        public sealed class __TYPE__ : IPlugin
        {
            public static string? LastValue;

            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
            {
                LastValue = context.TryGet<object>("fs")?.ToString();
                return Task.CompletedTask;
            }

            public Task DisposeAsync() => Task.CompletedTask;
        }
        """;

    private static string Provider(string id, string value)
        => ProviderTemplate.Replace("__TYPE__", $"Provider_{id}").Replace("__VALUE__", value);

    private static string Consumer(string id)
        => ConsumerTemplate.Replace("__TYPE__", $"Consumer_{id}");

    /// <summary>providers 提供 fs（各自带值）；resolvers 无门控纯解析（证明域内容）；其余为门控消费者。</summary>
    private static KeystoneHostOptions Options(
        (string Id, string Value)[] providers, string[] resolvers)
    {
        var values = providers.ToDictionary(p => p.Id, p => p.Value, StringComparer.Ordinal);
        var resolverSet = resolvers.ToHashSet(StringComparer.Ordinal);
        return new KeystoneHostOptions
        {
            ManifestProvider = e => new PluginManifest(
                e.Id!, "1.0.0", "P.cs", ["cordis-runtime"],
                values.ContainsKey(e.Id!) ? ["fs"] : [],
                values.ContainsKey(e.Id!) || resolverSet.Contains(e.Id!) ? [] : ["fs"]),
            SourceProvider = e => new Keystone.Runtime.Plugins.Loading.PluginSource(
                e.Id!,
                values.TryGetValue(e.Id!, out var value) ? Provider(e.Id!, value) : Consumer(e.Id!)),
        };
    }

    /// <summary>跨 ALC 读静态（重载后新旧副本短暂并存 → Any 命中即证明；P57-T4 语义）。</summary>
    private static List<string?> ReadLastValues(string typeName)
    {
        var values = new List<string?>();
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetTypes().FirstOrDefault(x => x.Name == typeName);
            var field = t?.GetField("LastValue", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (field is not null)
            {
                values.Add((string?)field.GetValue(null));
            }
        }

        return values;
    }

    private static string AssertResolved(string entryId)
    {
        var values = ReadLastValues($"Consumer_{entryId}");
        return values.FirstOrDefault(v => v is not null)
            ?? throw new InvalidOperationException($"consumer '{entryId}' resolved nothing");
    }

    private static async Task WaitForStateAsync(KeystoneHost host, string id, PluginLifecycleState expected)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (host.GetPluginState(id) != expected)
        {
            cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task Group_private_isolate_routes_value_by_realm()
    {
        // 组声明 isolate {fs: true} → 域 #g：组内消费者取组内值；组外消费者取共享值（双向路由证明）
        await using var host = new KeystoneHost(Options([("gp_p", "group-val"), ("p2", "shared-val")], []));
        await host.StartAsync("""
            - id: g
              name: ./plugins/g
              isolate:
                fs: true
              group:
                - id: gp_p
                  name: ./plugins/p
                - id: gp_c
                  name: ./plugins/c
                  inject: [fs]
            - id: p2
              name: ./plugins/p2
            - id: gp_out
              name: ./plugins/outsider
              inject: [fs]
            """);

        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("gp_p"));
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("p2"));
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("gp_c"));
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("gp_out"));
        Assert.Equal("group-val", AssertResolved("gp_c")); // c 门控+解析都在 #g → 组内值
        Assert.Equal("shared-val", AssertResolved("gp_out")); // outsider 在 "" → 共享值（#g 不外泄）

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task Named_label_routes_value_by_label_realm()
    {
        // @label 命名共享域：同 label 跨组互见（且不串到异 label）
        await using var host = new KeystoneHost(Options([("nl_p", "alpha-val"), ("nl_p3", "beta-val")], []));
        await host.StartAsync("""
            - id: g1
              name: ./plugins/g1
              isolate:
                fs: alpha
              group:
                - id: nl_p
                  name: ./plugins/p
            - id: g2
              name: ./plugins/g2
              isolate:
                fs: alpha
              group:
                - id: nl_c1
                  name: ./plugins/c1
                  inject: [fs]
            - id: g3
              name: ./plugins/g3
              isolate:
                fs: beta
              group:
                - id: nl_p3
                  name: ./plugins/p3
                - id: nl_c2
                  name: ./plugins/c2
                  inject: [fs]
            """);

        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("nl_c1"));
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("nl_c2"));
        Assert.Equal("alpha-val", AssertResolved("nl_c1")); // @alpha 路由
        Assert.Equal("beta-val", AssertResolved("nl_c2")); // @beta 路由（不串 alpha）

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task Entry_level_private_isolates_each_declarer()
    {
        // 叶自声明 isolate {fs: true} → 各自 #ownId：ep_q(#q) 解析落空（#p 与 "" 都不泄漏）；无声明 ep_r 走 ""
        await using var host = new KeystoneHost(Options([("ep_p", "p-val"), ("p2", "shared-val")], ["ep_q", "ep_r"]));
        await host.StartAsync("""
            - id: ep_p
              name: ./plugins/p
              isolate:
                fs: true
            - id: p2
              name: ./plugins/p2
            - id: ep_q
              name: ./plugins/q
              isolate:
                fs: true
            - id: ep_r
              name: ./plugins/r
            """);

        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("ep_p"));
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("ep_q"));
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("ep_r"));

        var qValues = ReadLastValues("Consumer_ep_q");
        Assert.All(qValues, v => Assert.Null(v)); // #q 空：#p 的值与 "" 的值都不泄漏
        Assert.Equal("shared-val", AssertResolved("ep_r")); // 无声明 → "" 域

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task Isolate_removal_shifts_realm_and_reloads_descendants()
    {
        // F10 域迁移：初始 g{isolate fs:alpha}（rm_p 值在 @alpha）+ p2 共享域兜底；新树移除 g.isolate 且移除 p2
        // → rm_p/rm_c 冷重启，rm_p 值入 ""；rm_fresh（一直门控 ""）从 shared-val 翻到 p-val（域迁移端到端）
        await using var host = new KeystoneHost(Options([("rm_p", "p-val"), ("p2", "shared-val")], []));
        var reloaded = new List<string>();
        host.PluginReloading += (_, e) => reloaded.Add(e.EntryId);

        await host.StartAsync("""
            - id: g
              name: ./plugins/g
              isolate:
                fs: alpha
              group:
                - id: rm_p
                  name: ./plugins/p
                - id: rm_c
                  name: ./plugins/c
                  inject: [fs]
            - id: p2
              name: ./plugins/p2
            - id: rm_fresh
              name: ./plugins/fresh
              inject: [fs]
            """);

        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("rm_c")); // @alpha 门控满足
        Assert.Equal("p-val", AssertResolved("rm_c"));
        Assert.Equal("shared-val", AssertResolved("rm_fresh")); // 初始 "" 由 p2 兜底

        await host.ApplyConfigAsync(Keystone.Config.Entries.EntryParser.Parse("""
            - id: g
              name: ./plugins/g
              group:
                - id: rm_p
                  name: ./plugins/p
                - id: rm_c
                  name: ./plugins/c
                  inject: [fs]
            - id: rm_fresh
              name: ./plugins/fresh
              inject: [fs]
            """));

        Assert.Contains("rm_p", reloaded); // 组级声明变化 → 组内叶子生效键变 → 冷重启
        Assert.Contains("rm_c", reloaded);
        Assert.DoesNotContain("rm_fresh", reloaded); // fresh 无结构变（域始终 ""）→ 不重载，走依赖重评

        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("rm_p"));
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("rm_c"));
        await WaitForStateAsync(host, "rm_fresh", PluginLifecycleState.Active); // p2 摘除→p 入 "" → G-C2 重臂
        Assert.Contains("p-val", ReadLastValues("Consumer_rm_fresh")); // 域迁移证明：fresh 现取到 p 的值

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task Label_change_reloads_and_keeps_dependent_active()
    {
        // F10：label alpha → beta：双方整体迁入 @beta → 重载后依赖仍满足（域随配置迁移，不悬死）
        await using var host = new KeystoneHost(Options([("lc_p", "p-val")], []));
        var reloaded = new List<string>();
        host.PluginReloading += (_, e) => reloaded.Add(e.EntryId);

        await host.StartAsync("""
            - id: g1
              name: ./plugins/g1
              isolate:
                fs: alpha
              group:
                - id: lc_p
                  name: ./plugins/p
            - id: g2
              name: ./plugins/g2
              isolate:
                fs: alpha
              group:
                - id: lc_c1
                  name: ./plugins/c1
                  inject: [fs]
            """);

        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("lc_c1"));

        await host.ApplyConfigAsync(Keystone.Config.Entries.EntryParser.Parse("""
            - id: g1
              name: ./plugins/g1
              isolate:
                fs: beta
              group:
                - id: lc_p
                  name: ./plugins/p
            - id: g2
              name: ./plugins/g2
              isolate:
                fs: beta
              group:
                - id: lc_c1
                  name: ./plugins/c1
                  inject: [fs]
            """));

        Assert.Contains("lc_p", reloaded);
        Assert.Contains("lc_c1", reloaded);
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("lc_p"));
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("lc_c1")); // 同迁 @beta → 仍互见
        Assert.Contains("p-val", ReadLastValues("Consumer_lc_c1"));

        await host.ShutdownAsync();
    }
}
