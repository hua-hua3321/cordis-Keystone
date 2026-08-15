using Keystone.Config.Entries;

namespace Keystone.Config.Tests;

public class EntryChangeClassifierTests
{
    private static EntryOptions Entry(string? name = null, string? inject = null, bool? group = null, bool? disabled = null)
        => new()
        {
            Id = "e",
            Name = name ?? "./plugins/e",
            Config = null,
            Disabled = disabled,
            Inject = inject is null ? [] : [inject],
            Group = group is null ? null : [],
        };

    [Fact]
    public void No_change_is_none()
    {
        var e = Entry();

        Assert.Equal(EntryChangeAction.None, EntryChangeClassifier.Classify(e, e with { }));
    }

    [Fact]
    public void Config_only_change_is_hot_update()
    {
        var before = Entry();
        var after = before with { Config = new Dictionary<string, object?> { ["k"] = "v" } };

        Assert.Equal(EntryChangeAction.HotUpdate, EntryChangeClassifier.Classify(before, after));
    }

    [Fact]
    public void Name_change_is_restart()
    {
        var before = Entry(name: "./plugins/a");
        var after = before with { Name = "./plugins/b" };

        Assert.Equal(EntryChangeAction.Restart, EntryChangeClassifier.Classify(before, after));
    }

    [Fact]
    public void Inject_change_is_restart()
    {
        var before = Entry(inject: "llm");
        var after = before with { Inject = ["telemetry"] };

        Assert.Equal(EntryChangeAction.Restart, EntryChangeClassifier.Classify(before, after));
    }

    [Fact]
    public void Group_structure_change_is_restart()
    {
        var before = Entry();
        var after = before with { Group = [Entry() with { Id = "child" }] };

        Assert.Equal(EntryChangeAction.Restart, EntryChangeClassifier.Classify(before, after));
    }

    [Fact]
    public void Disabled_flip_is_dispose_only()
    {
        var before = Entry(disabled: false);
        var after = before with { Disabled = true };

        Assert.Equal(EntryChangeAction.DisposeOnly, EntryChangeClassifier.Classify(before, after));
    }
}
