using Keystone.Core.Contracts;

namespace Keystone.Core.Tests;

public class TaskIdTests
{
    [Fact]
    public void New_returns_unique_values()
    {
        var seen = new HashSet<TaskId>();
        for (var i = 0; i < 1000; i++)
        {
            Assert.True(seen.Add(TaskId.New()));
        }
    }

    [Fact]
    public void CreateChild_returns_distinct_value()
    {
        var parent = TaskId.New();
        var child = TaskId.CreateChild();

        Assert.NotEqual(parent, child);
        Assert.NotEqual(Guid.Empty, child.Value);
    }

    [Fact]
    public void Parse_roundtrips_ToString()
    {
        var id = TaskId.New();

        var parsed = TaskId.Parse(id.ToString());

        Assert.Equal(id, parsed);
    }

    [Fact]
    public void TryParse_accepts_valid_and_rejects_invalid()
    {
        Assert.True(TaskId.TryParse(Guid.NewGuid().ToString(), out var valid));
        Assert.NotEqual(default, valid);

        Assert.False(TaskId.TryParse("not-a-guid", out var invalid));
        Assert.Equal(default, invalid);
    }

    [Fact]
    public void Value_equality_is_structural()
    {
        var guid = Guid.NewGuid();

        Assert.Equal(new TaskId(guid), new TaskId(guid));
        Assert.NotEqual(new TaskId(guid), new TaskId(Guid.NewGuid()));
    }

    [Fact]
    public void CompareTo_orders_by_guid_value()
    {
        var low = new TaskId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var high = new TaskId(Guid.Parse("00000000-0000-0000-0000-000000000002"));

        Assert.True(low.CompareTo(high) < 0);
        Assert.True(high.CompareTo(low) > 0);
        Assert.Equal(0, low.CompareTo(new TaskId(low.Value)));
    }
}
