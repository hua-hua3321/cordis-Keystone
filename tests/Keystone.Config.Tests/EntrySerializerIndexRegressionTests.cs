using Keystone.Config.Entries;
using Keystone.Config.Persistence;

namespace Keystone.Config.Tests;

/// <summary>
/// DC-15 回归（P45）：Serialize 方法组曾绑定 Select 的 (T,int) 索引重载——
/// 元素下标灌进 indent（第 N 条目缩进 N 空格），多条目写回损坏。
/// </summary>
public class EntrySerializerIndexRegressionTests
{
    [Fact]
    public async Task Multiple_entries_serialize_with_zero_indent_and_roundtrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"keystone-ser-{Guid.NewGuid():N}.yml");
        try
        {
            var writer = new ConfigFileWriter(path);
            writer.ScheduleWrite(
            [
                new EntryOptions { Id = "a", Name = "./a" },
                new EntryOptions { Id = "b", Name = "./b" },
                new EntryOptions { Id = "c", Name = "./c" },
            ]);
            await writer.FlushAsync();

            var raw = await File.ReadAllTextAsync(path);
            Assert.DoesNotContain(" - id", raw); // 无下标缩进泄漏（回归断言）
            var parsed = EntryParser.Parse(raw);
            Assert.Equal(["a", "b", "c"], parsed.Select(e => e.Id));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
