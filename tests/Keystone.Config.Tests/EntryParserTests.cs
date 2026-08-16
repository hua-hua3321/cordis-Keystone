using Keystone.Config.Entries;

namespace Keystone.Config.Tests;

public class EntryParserTests
{
    [Fact]
    public void Parses_simple_entry()
    {
        var entries = EntryParser.Parse("""
            - id: fs
              name: ./plugins/fs
              config:
                root: /data
              disabled: false
            """);

        var entry = Assert.Single(entries);
        Assert.Equal("fs", entry.Id);
        Assert.Equal("./plugins/fs", entry.Name);
        Assert.False(entry.Disabled);
        Assert.IsType<Dictionary<string, object?>>(entry.Config);
    }

    [Fact]
    public void Parses_inject_and_isolate()
    {
        var entries = EntryParser.Parse("""
            - id: ai
              name: ./plugins/ai
              inject: [llm, telemetry]
              isolate: [fs]
            """);

        var entry = Assert.Single(entries);
        Assert.Equal(["llm", "telemetry"], entry.Inject);
        // 列表 shim ≡ 全私有（18 §2 CA-1 第 0 步）
        var isolate = Assert.Single(entry.Isolate);
        Assert.Equal("fs", isolate.Key);
        Assert.Equal(IsolateKind.Private, isolate.Value.Kind);
    }

    [Fact]
    public void Parses_nested_group()
    {
        var entries = EntryParser.Parse("""
            - id: auth
              group:
                - id: auth-login
                  name: ./plugins/auth-login
            """);

        var group = Assert.Single(entries);
        Assert.True(group.IsGroup);
        var child = Assert.Single(group.Group!);
        Assert.Equal("auth-login", child.Id);
    }

    [Fact]
    public void Duplicate_id_fails_fast()
    {
        Assert.Throws<Keystone.Core.Errors.KeystoneException>(() => EntryParser.Parse("""
            - id: dup
              name: ./a
            - id: dup
              name: ./b
            """));
    }
}
