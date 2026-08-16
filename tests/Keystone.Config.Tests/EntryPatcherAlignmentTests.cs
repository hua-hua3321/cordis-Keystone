using Keystone.Config.Entries;

namespace Keystone.Config.Tests;

/// <summary>
/// P2-2（19 号审计 LD-12，对齐 include/src/index.ts applyEntryPatches）：
/// a) 无 patch 也 detached（structuredClone 语义——返回树不共享入参可变性）；
/// c) insert 与 overrides 互斥（Cordis insert 分支 continue）；
/// b) Disabled=false 显式清除（bool? false 非 null → 赋值——对齐 disabled:false 清挂起）。
/// </summary>
public class EntryPatcherAlignmentTests
{
    private static readonly IReadOnlyList<EntryOptions> Tree =
    [
        new() { Id = "a", Name = "./a", Config = new Dictionary<string, object?> { ["k"] = 1 } },
        new()
        {
            Id = "g", Name = "./g", Group =
            [
                new() { Id = "c", Name = "./c", Disabled = true },
            ],
        },
    ];

    [Fact]
    public void Empty_patches_returns_detached_copy()
    {
        // a) Cordis: structuredClone 后原样返回——无 patch 也不共享引用
        var result = EntryPatcher.Apply(Tree, []);

        Assert.NotSame(Tree, result); // 新列表
        Assert.Equal(Tree.Count, result.Count);
        Assert.NotSame(Tree[0], result[0]); // 条目也是副本（修改入参不影响结果）
    }

    [Fact]
    public void Insert_and_overrides_are_mutually_exclusive()
    {
        // c) 同一 patch 同时带 Insert 与 Overrides：Cordis insert 分支 continue——overrides 不生效
        var patch = new EntryPatch(
            GroupId: null,
            Insert: [new() { Id = "new", Name = "./new" }],
            Overrides: new Dictionary<string, EntryOptions>
            {
                ["a"] = new() { Id = "a", Name = "./a", Config = new Dictionary<string, object?> { ["k"] = 99 } },
            });

        var result = EntryPatcher.Apply(Tree, [patch]);

        Assert.Contains(result, e => e.Id == "new"); // insert 生效
        var a = result.Single(e => e.Id == "a");
        var config = Assert.IsAssignableFrom<Dictionary<string, object?>>(a.Config);
        Assert.Equal(1, config["k"]); // overrides 被跳过（互斥）
    }

    [Fact]
    public void Override_disabled_false_clears_suspension()
    {
        // b) disabled: false 显式清除（对齐 Cordis disabled 键赋值——false 覆盖 true）
        var patch = new EntryPatch(
            GroupId: null,
            Insert: null,
            Overrides: new Dictionary<string, EntryOptions>
            {
                ["c"] = new() { Id = "c", Name = "./c", Disabled = false },
            });

        var result = EntryPatcher.Apply(Tree, [patch]);
        var group = result.Single(e => e.Id == "g");
        var c = group.Group!.Single(e => e.Id == "c");

        Assert.NotEqual(true, c.Disabled); // false 已赋值（清除挂起）
    }
}
