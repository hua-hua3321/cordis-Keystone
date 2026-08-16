using Keystone.Config.Entries;

namespace Keystone.Config.Tests;

/// <summary>
/// CA-5 运行期 patch 注入（18 §2 P2，P61，对齐 Cordis include Config.patches 读后插入）：
/// Config 层纯函数 EntryPatcher.Apply——插入组（GroupId 非空）或根 + 覆盖按 id 合并非 null 字段；
/// name 不匹配跳过 + onWarn 回调。注意与 PatchContextAsync（上下文补丁瀑布）语义不同。
/// </summary>
public class EntryPatcherTests
{
    private static IReadOnlyList<EntryOptions> Parse(string yaml) => EntryParser.Parse(yaml);

    [Fact]
    public void Patch_inserts_into_root()
    {
        // 插入根：GroupId null → 追加到根
        var tree = Parse("- id: a\n  name: ./a\n");
        var patches = new List<EntryPatch>
        {
            new(GroupId: null, Insert: [new EntryOptions { Id = "b", Name = "./b" }], Overrides: null),
        };

        var patched = EntryPatcher.Apply(tree, patches);

        Assert.Equal(["a", "b"], patched.Select(e => e.Id));
    }

    [Fact]
    public void Patch_inserts_into_group()
    {
        // 插入组：GroupId 指向组条目 → 子叶插入该组
        var tree = Parse("""
            - id: g
              name: ./g
              group:
                - id: existing
                  name: ./existing
            """);
        var patches = new List<EntryPatch>
        {
            new(GroupId: "g", Insert: [new EntryOptions { Id = "child", Name = "./child" }], Overrides: null),
        };

        var patched = EntryPatcher.Apply(tree, patches);

        var group = Assert.Single(patched);
        Assert.Equal(["existing", "child"], group.Group!.Select(e => e.Id));
    }

    [Fact]
    public void Patch_overrides_by_id_merging_non_null_fields()
    {
        // 覆盖：按 id 合并（提供的字段覆盖，未提供保留）
        var tree = Parse("- id: a\n  name: ./a\n  config:\n    k: 1\n");
        var patches = new List<EntryPatch>
        {
            new(GroupId: null, Insert: null,
                Overrides: new Dictionary<string, EntryOptions>
                {
                    ["a"] = new EntryOptions { Id = "a", Config = new Dictionary<string, object?> { ["j"] = 2 } },
                }),
        };

        var patched = EntryPatcher.Apply(tree, patches);

        var entry = Assert.Single(patched);
        Assert.Equal("./a", entry.Name); // 未提供 name 保留
        var config = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(entry.Config);
        Assert.False(config.ContainsKey("k")); // config 是条目级字段：整体替换（浅合并——对齐 Cordis entry 级 patch）
        Assert.True(config.ContainsKey("j")); // override 的 config 生效
    }

    [Fact]
    public void Patch_name_mismatch_skips_with_warning()
    {
        // name 不匹配 → 跳过 + onWarn 回调（对齐 Cordis patch 按 name 匹配语义）
        var tree = Parse("- id: a\n  name: ./a\n");
        var warnings = new List<string>();
        var patches = new List<EntryPatch>
        {
            new(GroupId: "nonexistent", Insert: [new EntryOptions { Id = "x", Name = "./x" }], Overrides: null),
        };

        var patched = EntryPatcher.Apply(tree, patches, onWarn: warnings.Add);

        Assert.Single(patched); // 未插入
        Assert.Contains(warnings, w => w.Contains("nonexistent")); // 组不存在警告
    }

    [Fact]
    public void Empty_patches_is_content_identity_but_detached()
    {
        // P2-2a（19 号审计 LD-12，对齐 include structuredClone）：空 patches 内容恒等但 detached
        //（修复前同引用——入参可变性会穿透到结果）
        var tree = Parse("- id: a\n  name: ./a\n");

        var patched = EntryPatcher.Apply(tree, []);

        Assert.NotSame(tree, patched); // detached
        Assert.Equal(tree.Count, patched.Count);
        Assert.Equal(tree[0].Id, patched[0].Id);
        Assert.Equal(tree[0].Name, patched[0].Name);
    }
}
