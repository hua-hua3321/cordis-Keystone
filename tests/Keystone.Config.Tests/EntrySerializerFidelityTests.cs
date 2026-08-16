using Keystone.Config.Entries;
using Keystone.Config.Persistence;

namespace Keystone.Config.Tests;

/// <summary>
/// P68（19 号审计 P2-28）：EntrySerializer 回写保真——修复前 list 形状三失真：
/// ① 字典列表重复键（`key:` 多次 → 重解析只留最后一项，数据丢失）；
/// ② 特殊字符标量无引号（`:`/`#`/前后空格 → 重解析错切/变注释）；
/// ③ 空容器塌缩（空 dict/list 输出空 → 重解析变 null）。
/// </summary>
public class EntrySerializerFidelityTests
{
    private static Dictionary<string, object?> RoundTrip(Dictionary<string, object?> config)
    {
        var entries = new List<EntryOptions>
        {
            new() { Id = "a", Name = "./a", Config = config },
        };
        var yaml = EntrySerializer.Serialize(entries);
        var parsed = EntryParser.Parse(yaml);
        return (Dictionary<string, object?>)parsed[0].Config!;
    }

    [Fact]
    public void List_of_dicts_roundtrips_all_items()
    {
        var config = new Dictionary<string, object?>
        {
            ["items"] = new List<object?>
            {
                new Dictionary<string, object?> { ["id"] = "one", ["v"] = "1" },
                new Dictionary<string, object?> { ["id"] = "two", ["v"] = "2" },
            },
        };

        var result = RoundTrip(config);

        var items = Assert.IsAssignableFrom<IReadOnlyList<object?>>(result["items"]);
        Assert.Equal(2, items.Count); // 修复前重复键塌缩只留 1 项
        var first = Assert.IsAssignableFrom<Dictionary<string, object?>>(items[0]);
        Assert.Equal("one", first["id"]);
        var second = Assert.IsAssignableFrom<Dictionary<string, object?>>(items[1]);
        Assert.Equal("two", second["id"]);
    }

    [Theory]
    [InlineData("a: b")]   // 冒号+空格 = 映射分隔符
    [InlineData("a #b")]   // 井号 = 注释起始
    [InlineData(" leading")]  // 前导空格被裁
    [InlineData("trailing ")] // 尾空格被裁
    public void Special_scalars_roundtrip_unchanged(string value)
    {
        var result = RoundTrip(new Dictionary<string, object?> { ["k"] = value });

        Assert.Equal(value, result["k"]); // 修复前重解析错切/裁剪
    }

    [Fact]
    public void Empty_containers_survive_roundtrip()
    {
        var result = RoundTrip(new Dictionary<string, object?>
        {
            ["emptyMap"] = new Dictionary<string, object?>(),
            ["emptyList"] = new List<object?>(),
        });

        var map = Assert.IsAssignableFrom<Dictionary<string, object?>>(result["emptyMap"]);
        Assert.Empty(map); // 修复前塌缩为 null（KeyNotFoundException）
        var list = Assert.IsAssignableFrom<IReadOnlyList<object?>>(result["emptyList"]);
        Assert.Empty(list);
    }
}
