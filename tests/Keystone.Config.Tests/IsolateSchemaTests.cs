using Keystone.Config.Entries;
using Keystone.Config.Persistence;
using Keystone.Core.Errors;

namespace Keystone.Config.Tests;

/// <summary>
/// isolate schema 对齐 Cordis（18 §2 CA-1 第 0 步）：
/// map 两档 Dict&lt;name → true|"label"&gt;（true=私有域 / 字符串=命名共享域）+ 列表 shim（≡ 全私有）；
/// "false" = 显式解除（分层补丁撤销底层声明）；非法形态 fail-fast。
/// </summary>
public class IsolateSchemaTests
{
    [Fact]
    public void Map_form_parses_two_tiers()
    {
        var entries = EntryParser.Parse("""
            - id: app
              isolate:
                fs: true
                cache: shared-a
            """);

        var entry = Assert.Single(entries);
        Assert.Equal(2, entry.Isolate.Count);
        Assert.Equal(IsolateKind.Private, entry.Isolate["fs"].Kind);
        Assert.Null(entry.Isolate["fs"].Label);
        Assert.Equal(IsolateKind.Shared, entry.Isolate["cache"].Kind);
        Assert.Equal("shared-a", entry.Isolate["cache"].Label);
    }

    [Fact]
    public void List_form_shim_expands_to_private()
    {
        var entries = EntryParser.Parse("""
            - id: app
              isolate: [fs, cache]
            """);

        var entry = Assert.Single(entries);
        Assert.Equal(2, entry.Isolate.Count);
        Assert.All(entry.Isolate.Values, spec => Assert.Equal(IsolateKind.Private, spec.Kind));
    }

    [Fact]
    public void Map_false_parses_as_explicit_none()
    {
        var entries = EntryParser.Parse("""
            - id: app
              isolate:
                fs: false
            """);

        var entry = Assert.Single(entries);
        var fs = Assert.Single(entry.Isolate);
        Assert.Equal("fs", fs.Key);
        Assert.Equal(IsolateKind.None, fs.Value.Kind);
    }

    [Fact]
    public void Absent_isolate_defaults_to_empty()
    {
        var entries = EntryParser.Parse("- id: app");

        var entry = Assert.Single(entries);
        Assert.Empty(entry.Isolate);
    }

    [Fact]
    public void Scalar_isolate_shape_fails_fast()
    {
        var exception = Assert.Throws<KeystoneException>(() => EntryParser.Parse("""
            - id: app
              isolate: 42
            """));
        Assert.Equal(ErrorCode.ConfigValidationFailed, exception.Code);
    }

    [Fact]
    public void Empty_isolate_label_fails_fast()
    {
        var exception = Assert.Throws<KeystoneException>(() => EntryParser.Parse("""
            - id: app
              isolate:
                fs: ""
            """));
        Assert.Equal(ErrorCode.ConfigValidationFailed, exception.Code);
    }

    [Fact]
    public void Non_scalar_map_value_fails_fast()
    {
        var exception = Assert.Throws<KeystoneException>(() => EntryParser.Parse("""
            - id: app
              isolate:
                fs: [a]
            """));
        Assert.Equal(ErrorCode.ConfigValidationFailed, exception.Code);
    }

    [Fact]
    public void Non_scalar_list_item_fails_fast()
    {
        var exception = Assert.Throws<KeystoneException>(() => EntryParser.Parse("""
            - id: app
              isolate:
                - fs
                - [nested]
            """));
        Assert.Equal(ErrorCode.ConfigValidationFailed, exception.Code);
    }

    [Fact]
    public void Serialize_writes_map_form_and_roundtrips()
    {
        var entries = EntryParser.Parse("""
            - id: app
              isolate:
                fs: true
                cache: shared-a
            """);

        var yaml = EntrySerializer.Serialize(entries);
        Assert.Contains("isolate:", yaml);
        Assert.Contains("fs: true", yaml);
        Assert.Contains("cache: shared-a", yaml);

        // roundtrip：重解析后两档语义等值
        var reparsed = Assert.Single(EntryParser.Parse(yaml));
        Assert.Equal(IsolateKind.Private, reparsed.Isolate["fs"].Kind);
        Assert.Equal(IsolateKind.Shared, reparsed.Isolate["cache"].Kind);
        Assert.Equal("shared-a", reparsed.Isolate["cache"].Label);
    }

    [Fact]
    public void Layered_merge_overrides_per_name_and_false_removes()
    {
        var @base = new List<EntryOptions>
        {
            new()
            {
                Id = "app",
                Isolate = new Dictionary<string, IsolateSpec>(StringComparer.Ordinal)
                {
                    ["fs"] = IsolateSpec.Private(),
                    ["cache"] = IsolateSpec.Shared("old-label"),
                },
            },
        };
        var patchSameId = new List<EntryOptions>
        {
            new()
            {
                Id = "app",
                Isolate = new Dictionary<string, IsolateSpec>(StringComparer.Ordinal)
                {
                    ["fs"] = IsolateSpec.Shared("shared-x"), // 覆盖同名：true → 命名共享
                },
            },
        };

        var merged = EntryTree.ApplyLayers([@base, patchSameId]);
        var entry = Assert.Single(merged);
        Assert.Equal(2, entry.Isolate.Count);
        Assert.Equal(IsolateKind.Shared, entry.Isolate["fs"].Kind);
        Assert.Equal("shared-x", entry.Isolate["fs"].Label);
        Assert.Equal(IsolateKind.Shared, entry.Isolate["cache"].Kind); // 未提及保留底层
        Assert.Equal("old-label", entry.Isolate["cache"].Label);

        // false = 显式解除：fs 移除，其余保留
        var patchRemove = new List<EntryOptions>
        {
            new()
            {
                Id = "app",
                Isolate = new Dictionary<string, IsolateSpec>(StringComparer.Ordinal)
                {
                    ["fs"] = IsolateSpec.None(),
                },
            },
        };
        var removed = Assert.Single(EntryTree.ApplyLayers([@base, patchRemove]));
        var onlyCache = Assert.Single(removed.Isolate);
        Assert.Equal("cache", onlyCache.Key);
    }

    [Fact]
    public void Shared_factory_rejects_whitespace_label()
    {
        Assert.Throws<ArgumentException>(() => IsolateSpec.Shared(" "));
        Assert.Throws<ArgumentException>(() => IsolateSpec.Shared(""));
    }
}
