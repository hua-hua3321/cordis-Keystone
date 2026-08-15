using Keystone.Config.Entries;
using Keystone.Config.Interpolation;

namespace Keystone.Config.Tests;

/// <summary>
/// DC-8（17-doc-compliance-audit）：StaticInterpolator 接 EntryParser——YAML tag 语法
/// （ADR-0012/08 §4：!!env NAME / !!file path）+ 引用环检测 + 展开后参与校验。
/// 修复前：EntryParser 丢 tag（NodeToObject 只取 scalar.Value），StaticInterpolator 零调用。
/// </summary>
public class EntryParserInterpolationTests
{
    private static StaticInterpolator Interpolator(
        Func<string, string?>? env = null,
        Func<string, string?>? file = null)
        => new(env ?? (_ => null), file ?? (_ => null));

    [Fact]
    public void Env_tag_is_expanded_in_entry_config()
    {
        var interpolator = Interpolator(env: name => name == "PLUGIN_DATA_DIR" ? "/custom/data" : null);

        var entries = EntryParser.Parse("""
            - id: fs
              name: ./plugins/fs
              config:
                root: !!env PLUGIN_DATA_DIR
            """, interpolator);

        var config = (Dictionary<string, object?>)Assert.Single(entries).Config!;
        Assert.Equal("/custom/data", config["root"]);
    }

    [Fact]
    public void File_tag_inlines_content_with_recursive_interpolation()
    {
        // !!file 内容递归插值（内容为 !!env:NAME 整值标记——文本内容形态）
        var interpolator = Interpolator(
            env: name => name == "TOKEN" ? "secret-token" : null,
            file: path => path == "./defaults.yaml" ? "!!env:TOKEN" : null);

        var entries = EntryParser.Parse("""
            - id: fs
              name: ./plugins/fs
              config:
                defaults: !!file ./defaults.yaml
            """, interpolator);

        var config = (Dictionary<string, object?>)Assert.Single(entries).Config!;
        Assert.Equal("secret-token", config["defaults"]);
    }

    [Fact]
    public void Interpolation_applies_to_nested_structures()
    {
        var interpolator = Interpolator(
            env: name => name == "URL" ? "http://127.0.0.1:8080" : name == "HOST" ? "127.0.0.1" : null);

        var entries = EntryParser.Parse("""
            - id: web
              name: ./plugins/web
              config:
                endpoints:
                  - url: !!env URL
                nested:
                  deep: !!env HOST
            """, interpolator);

        var config = (Dictionary<string, object?>)Assert.Single(entries).Config!;
        var endpoints = (List<object?>)config["endpoints"]!;
        var first = (Dictionary<string, object?>)endpoints[0]!;
        Assert.Equal("http://127.0.0.1:8080", first["url"]);
        var nested = (Dictionary<string, object?>)config["nested"]!;
        Assert.Equal("127.0.0.1", nested["deep"]);
    }

    [Fact]
    public void Cyclic_file_reference_fails_fast()
    {
        // a.yaml → b.yaml → a.yaml：tag 语法 + 文本内容冒号引用混合环
        var interpolator = Interpolator(file: path => path switch
        {
            "a.yaml" => "!!file:b.yaml",
            "b.yaml" => "!!file:a.yaml",
            _ => null,
        });

        var exception = Assert.Throws<Keystone.Core.Errors.KeystoneException>(() => EntryParser.Parse("""
            - id: fs
              name: ./plugins/fs
              config:
                data: !!file a.yaml
            """, interpolator));

        Assert.Equal(Keystone.Core.Errors.ErrorCode.ConfigValidationFailed, exception.Code);
    }

    [Fact]
    public void Missing_env_keeps_tag_marker()
    {
        var interpolator = Interpolator(env: _ => null);

        var entries = EntryParser.Parse("""
            - id: fs
              name: ./plugins/fs
              config:
                root: !!env PLUGIN_DATA_DIR
            """, interpolator);

        var config = (Dictionary<string, object?>)Assert.Single(entries).Config!;
        Assert.Equal("!!env PLUGIN_DATA_DIR", config["root"]); // 缺失保留标记（不静默替换）
    }

    [Fact]
    public void Missing_file_keeps_tag_marker()
    {
        var interpolator = Interpolator(file: _ => null);

        var entries = EntryParser.Parse("""
            - id: fs
              name: ./plugins/fs
              config:
                defaults: !!file ./defaults.yaml
            """, interpolator);

        var config = (Dictionary<string, object?>)Assert.Single(entries).Config!;
        Assert.Equal("!!file ./defaults.yaml", config["defaults"]);
    }

    [Fact]
    public void Without_interpolator_tags_are_not_expanded()
    {
        // 兼容：无插值器 → 原行为（value 直取，tag 丢弃）
        var entries = EntryParser.Parse("""
            - id: fs
              name: ./plugins/fs
              config:
                root: !!env PLUGIN_DATA_DIR
            """);

        var config = (Dictionary<string, object?>)Assert.Single(entries).Config!;
        Assert.Equal("PLUGIN_DATA_DIR", config["root"]);
    }

    [Fact]
    public void Same_file_referenced_twice_is_not_a_cycle()
    {
        var interpolator = Interpolator(file: path => path == "shared.yaml" ? "shared-content" : null);

        var entries = EntryParser.Parse("""
            - id: a
              name: ./plugins/a
              config:
                x: !!file shared.yaml
            - id: b
              name: ./plugins/b
              config:
                y: !!file shared.yaml
            """, interpolator);

        Assert.Equal(2, entries.Count);
        var a = (Dictionary<string, object?>)entries[0].Config!;
        var b = (Dictionary<string, object?>)entries[1].Config!;
        Assert.Equal("shared-content", a["x"]);
        Assert.Equal("shared-content", b["y"]);
    }
}
