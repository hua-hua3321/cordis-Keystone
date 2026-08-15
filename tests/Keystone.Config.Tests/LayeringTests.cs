using Keystone.Config.Entries;
using Keystone.Config.Interpolation;

namespace Keystone.Config.Tests;

public class LayeringTests
{
    [Fact]
    public void Later_layer_overrides_earlier_by_id()
    {
        var baseLayer = EntryParser.Parse("""
            - id: fs
              name: ./plugins/fs
              config: { root: /data }
            """);
        var patch = EntryParser.Parse("""
            - id: fs
              config: { root: /new-data }
            """);

        var merged = EntryTree.ApplyLayers([baseLayer, patch]);

        var fs = Assert.Single(merged);
        Assert.Equal("./plugins/fs", fs.Name); // 补丁未改 name → 保留
        Assert.Equal("/new-data", ((Dictionary<string, object?>)fs.Config!)["root"]);
    }

    [Fact]
    public void Patch_can_insert_new_entries()
    {
        var baseLayer = EntryParser.Parse("- id: fs\n  name: ./plugins/fs\n");
        var patch = EntryParser.Parse("- id: telemetry\n  name: ./plugins/telemetry\n  insert: true\n");

        var merged = EntryTree.ApplyLayers([baseLayer, patch]);

        Assert.Equal(2, merged.Count);
        Assert.Contains(merged, e => e.Id == "telemetry");
    }

    [Fact]
    public void Duplicate_id_within_layer_fails_fast()
    {
        EntryOptions Entry(string id) => new() { Id = id, Name = $"./{id}" };
        var layer = new List<EntryOptions> { Entry("a"), Entry("a") };

        var exception = Assert.Throws<Keystone.Core.Errors.KeystoneException>(
            () => EntryTree.ApplyLayers([layer]));

        Assert.Equal(Keystone.Core.Errors.ErrorCode.ConfigValidationFailed, exception.Code);
    }

    [Fact]
    public void Patch_with_unknown_id_is_skipped()
    {
        var baseLayer = EntryParser.Parse("- id: fs\n  name: ./plugins/fs\n");
        var patch = EntryParser.Parse("- id: nonexistent\n  config: { x: 1 }\n");

        var merged = EntryTree.ApplyLayers([baseLayer, patch]); // 不抛，跳过未知 id 补丁

        Assert.Single(merged);
    }
}

public class StaticInterpolationTests
{
    [Fact]
    public void Env_variable_is_expanded()
    {
        var interpolator = new StaticInterpolator(
            env: name => name == "PLUGIN_DIR" ? "/custom/plugins" : null,
            file: _ => throw new InvalidOperationException("unexpected"));

        var result = interpolator.Interpolate("!!env:PLUGIN_DIR");

        Assert.Equal("/custom/plugins", result);
    }

    [Fact]
    public void Missing_env_leaves_value_unchanged()
    {
        var interpolator = new StaticInterpolator(env: _ => null, file: _ => throw new InvalidOperationException("unexpected"));

        Assert.Equal("!!env:MISSING", interpolator.Interpolate("!!env:MISSING"));
    }

    [Fact]
    public void File_content_is_inlined()
    {
        var interpolator = new StaticInterpolator(env: _ => null, file: path => path == "defaults.yaml" ? "default-content" : null);

        Assert.Equal("default-content", interpolator.Interpolate("!!file:defaults.yaml"));
    }

    [Fact]
    public void Cyclic_file_reference_fails_fast()
    {
        // A 引用 B，B 引用 A → 环 → 配置错误（ADR-0012 引用环检测）
        var interpolator = new StaticInterpolator(
            env: _ => null,
            file: path => path switch
            {
                "a.yaml" => "!!file:b.yaml",
                "b.yaml" => "!!file:a.yaml",
                _ => null,
            });

        var exception = Assert.Throws<Keystone.Core.Errors.KeystoneException>(
            () => interpolator.Interpolate("!!file:a.yaml"));

        Assert.Equal(Keystone.Core.Errors.ErrorCode.ConfigValidationFailed, exception.Code);
    }

    [Fact]
    public void Plain_strings_pass_through()
    {
        var interpolator = new StaticInterpolator(env: _ => null, file: _ => throw new InvalidOperationException("unexpected"));

        Assert.Equal("hello", interpolator.Interpolate("hello"));
    }
}
