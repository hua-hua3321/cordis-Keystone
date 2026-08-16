using Keystone.Core.Errors;
using Keystone.Runtime.Context;

namespace Keystone.Runtime.Tests;

/// <summary>
/// ContextFacade 键控 store 接线（18 §2 CA-1 第 1 步，P57-T3）：
/// facade 持共享 root KeyedServiceStore；realm 沿链推导（isolate map：名 → realm，子继承父、子影子覆盖父；
/// 无声明 → "" 默认共享）；Provide/Resolve 按 realm 写读共享 store；RemoveOwnedServices 走 disposer（幂等）。
/// </summary>
public class ContextFacadeIsolateTests
{
    private static ContextFacade NewRoot() => new("root");

    private static ContextFacade Child(
        ContextFacade parent,
        string name,
        IReadOnlyDictionary<string, string>? isolateMap = null)
        => new(name, parent, isolateMap: isolateMap);

    [Fact]
    public void Child_provide_is_visible_to_sibling_and_root()
    {
        var root = NewRoot();
        var a = Child(root, "plugin-a");
        var b = Child(root, "plugin-b");

        a.Provide("fs", "value-a");

        Assert.Equal("value-a", b.Get<string>("fs"));
        Assert.Equal("value-a", root.Get<string>("fs"));
        Assert.Equal("value-a", a.Get<string>("fs"));
    }

    [Fact]
    public void Owner_conflict_across_contexts_throws()
    {
        var root = NewRoot();
        var a = Child(root, "plugin-a");
        var b = Child(root, "plugin-b");
        a.Provide("fs", "value-a");

        var exception = Assert.Throws<KeystoneException>(() => b.Provide("fs", "value-b"));
        Assert.Equal(ErrorCode.ServiceAlreadyRegistered, exception.Code);
        Assert.Equal("value-a", a.Get<string>("fs"));
    }

    [Fact]
    public void Same_context_second_provide_throws_set_updates()
    {
        // D-6（P68）：二次 Provide 报错；更新走 Set
        var root = NewRoot();
        var a = Child(root, "plugin-a");
        a.Provide("fs", "old");

        Assert.Throws<Keystone.Core.Errors.KeystoneException>(() => a.Provide("fs", "new"));

        a.Set("fs", "new");
        Assert.Equal("new", root.Get<string>("fs"));
    }

    [Fact]
    public void RemoveOwnedServices_clears_values_and_is_idempotent()
    {
        var root = NewRoot();
        var a = Child(root, "plugin-a");
        a.Provide("fs", "value-a");
        a.Provide("cache", 42);

        a.RemoveOwnedServices();
        a.RemoveOwnedServices(); // 幂等

        Assert.Null(root.TryGet<string>("fs"));
        Assert.Null(root.TryGet<int?>("cache"));
    }

    [Fact]
    public void Private_isolate_hides_service_from_sibling_without_map()
    {
        var root = NewRoot();
        var isolated = Child(root, "plugin-a", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fs"] = "#group-1",
        });
        var plain = Child(root, "plugin-b");

        isolated.Provide("fs", "private-value");

        Assert.Equal("private-value", isolated.Get<string>("fs"));
        Assert.Null(plain.TryGet<string>("fs")); // 兄弟（无 map）落回默认共享域 → 不可见
        Assert.Null(root.TryGet<string>("fs"));
    }

    [Fact]
    public void Different_private_realms_are_isolated()
    {
        var root = NewRoot();
        var a = Child(root, "plugin-a", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fs"] = "#group-1",
        });
        var b = Child(root, "plugin-b", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fs"] = "#group-2",
        });

        a.Provide("fs", "from-a");
        b.Provide("fs", "from-b"); // 不同 realm → 不冲突

        Assert.Equal("from-a", a.Get<string>("fs"));
        Assert.Equal("from-b", b.Get<string>("fs"));
    }

    [Fact]
    public void Same_shared_label_sees_each_other()
    {
        var root = NewRoot();
        var a = Child(root, "plugin-a", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fs"] = "@label-x",
        });
        var b = Child(root, "plugin-b", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fs"] = "@label-x",
        });

        a.Provide("fs", "shared-value");

        Assert.Equal("shared-value", b.Get<string>("fs"));
        Assert.Null(root.TryGet<string>("fs")); // root（无 map）不可见
    }

    [Fact]
    public void Chain_inherits_parent_isolate_map()
    {
        var root = NewRoot();
        var mid = Child(root, "group-ctx", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fs"] = "#group-1",
        });
        var leaf = Child(mid, "plugin-a");

        leaf.Provide("fs", "group-private");

        Assert.Equal("group-private", leaf.Get<string>("fs"));
        Assert.Null(root.TryGet<string>("fs"));
    }

    [Fact]
    public void Child_map_shadows_parent_for_same_name()
    {
        var root = NewRoot();
        var mid = Child(root, "group-ctx", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fs"] = "@parent-label",
        });
        var leaf = Child(mid, "plugin-a", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fs"] = "#own",
        });
        var siblingUnderMid = Child(mid, "plugin-b");

        leaf.Provide("fs", "leaf-private");
        siblingUnderMid.Provide("fs", "group-shared");

        Assert.Equal("leaf-private", leaf.Get<string>("fs")); // 子 map 覆盖 → #own
        Assert.Equal("group-shared", siblingUnderMid.Get<string>("fs")); // 继承父 → @parent-label
    }

    [Fact]
    public void Per_name_isolation_leaves_other_names_shared()
    {
        var root = NewRoot();
        var a = Child(root, "plugin-a", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fs"] = "#group-1",
        });
        var b = Child(root, "plugin-b");

        a.Provide("fs", "private-fs");
        a.Provide("telemetry", "shared-telemetry");

        Assert.Null(b.TryGet<string>("fs")); // fs 隔离
        Assert.Equal("shared-telemetry", b.Get<string>("telemetry")); // 其余名仍共享
    }

    [Fact]
    public void Isolated_provider_conflicts_only_within_same_realm()
    {
        var root = NewRoot();
        var map = new Dictionary<string, string>(StringComparer.Ordinal) { ["fs"] = "#group-1" };
        var a = Child(root, "plugin-a", map);
        var b = Child(root, "plugin-b", new Dictionary<string, string>(map));

        a.Provide("fs", "from-a");

        Assert.Throws<KeystoneException>(() => b.Provide("fs", "from-b")); // 同 realm 属主冲突
    }

    [Fact]
    public async Task GetLazy_resolves_via_realm()
    {
        var root = NewRoot();
        var a = Child(root, "plugin-a", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fs"] = "@label-x",
        });
        var b = Child(root, "plugin-b", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fs"] = "@label-x",
        });

        a.Provide("fs", "lazy-value");

        Assert.Equal("lazy-value", await b.GetLazy<string>("fs").Value);
    }

    [Fact]
    public void Standalone_facade_owns_its_store()
    {
        var standalone1 = new ContextFacade("solo-1");
        var standalone2 = new ContextFacade("solo-2");

        standalone1.Provide("fs", "own-value");

        Assert.Equal("own-value", standalone1.Get<string>("fs"));
        Assert.Null(standalone2.TryGet<string>("fs")); // 独立 root：不共享
    }

    [Fact]
    public void Get_missing_throws_with_realm_context()
    {
        var root = NewRoot();
        var exception = Assert.Throws<KeystoneException>(() => root.Get<string>("fs"));
        Assert.Equal(ErrorCode.GatingServiceNotFound, exception.Code);
    }
}
