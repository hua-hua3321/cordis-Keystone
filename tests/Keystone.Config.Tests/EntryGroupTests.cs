using Keystone.Config.Entries;

namespace Keystone.Config.Tests;

public class EntryGroupTests
{
    private static EntryOptions Entry(string id) => new() { Id = id, Name = $"./plugins/{id}" };

    [Fact]
    public async Task Applies_all_entries_and_removes_disappeared()
    {
        var applied = new List<string>();
        var removed = new List<string>();
        var group = new EntryGroup(
            async e => applied.Add(e.Id!),
            async e => removed.Add(e.Id!));
        await group.UpdateAsync([Entry("a"), Entry("b")]);

        await group.UpdateAsync([Entry("a")]); // b 消失 → 卸载

        Assert.Equal(["a", "b", "a"], applied); // 第二次更新重新应用 a（仍在列表中）
        Assert.Equal(["b"], removed);
    }

    [Fact]
    public async Task Duplicate_id_detected_before_apply()
    {
        var applied = 0;

        await Assert.ThrowsAsync<Keystone.Core.Errors.KeystoneException>(async () =>
        {
            var g = new EntryGroup(_ => { applied++; return Task.CompletedTask; }, _ => Task.CompletedTask);
            await g.UpdateAsync([Entry("dup"), Entry("dup")]);
        });
        Assert.Equal(0, applied); // fail-fast：应用前检测
    }

    [Fact]
    public async Task Failure_rolls_back_new_entries_and_restores_old()
    {
        var applied = new List<string>();
        var removed = new List<string>();
        var group = new EntryGroup(
            async e =>
            {
                applied.Add(e.Id!);
                if (e.Id == "boom")
                {
                    throw new InvalidOperationException("apply failed");
                }
            },
            async e => removed.Add(e.Id!));

        await group.UpdateAsync([Entry("a")]);

        await Assert.ThrowsAsync<AggregateException>(() => group.UpdateAsync([Entry("a"), Entry("boom")]));

        // 回滚：boom 逆序卸载；旧 a 重建；boom 不残留
        Assert.Contains("boom", removed);
        Assert.Contains("a", applied);
        Assert.Equal([Entry("a").Id], group.Data.Select(e => e.Id));
    }

    [Fact]
    public async Task Unloading_tree_does_not_roll_back()
    {
        var applied = new List<string>();
        var removed = new List<string>();
        var group = new EntryGroup(
            async e =>
            {
                applied.Add(e.Id!);
                if (e.Id == "boom")
                {
                    throw new InvalidOperationException("apply failed");
                }
            },
            async e => removed.Add(e.Id!));

        group.MarkUnloaded(); // 树卸载中（F4：卸载主导终止，不回滚）

        await Assert.ThrowsAsync<AggregateException>(() => group.UpdateAsync([Entry("boom")]));

        Assert.Empty(removed); // 不回滚
    }

    [Fact]
    public async Task AwaitSettled_returns_after_all_applies()
    {
        var group = new EntryGroup(
            async e => await Task.Delay(20),
            _ => Task.CompletedTask);

        await group.UpdateAsync([Entry("a")]);
        await group.AwaitSettledAsync();

        Assert.Single(group.Data);
    }
}
