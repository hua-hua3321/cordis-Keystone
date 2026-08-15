using Keystone.Config.Interpolation;
using YamlDotNet.RepresentationModel;

namespace Keystone.Config.Entries;

/// <summary>
/// 条目 YAML 解析（08 §3）：YamlStream 节点树手动映射（规则 0：无反射反序列化器），
/// 顶层必须为列表；重复 id fail-fast（组级 + 顶层）。
/// 可传 <see cref="StaticInterpolator"/>（DC-8，ADR-0012）：非 null 时对 config 子树做静态插值——
/// <c>!!env NAME</c>/<c>!!file path</c> tag 展开（缺失保留标记）+ 引用环检测（跨整次解析共享 visited）。
/// </summary>
public static class EntryParser
{
    public static IReadOnlyList<EntryOptions> Parse(string yamlText, StaticInterpolator? interpolator = null)
    {
        ArgumentNullException.ThrowIfNull(yamlText);

        var stream = new YamlStream();
        stream.Load(new StringReader(yamlText));
        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is null)
        {
            return [];
        }

        if (stream.Documents[0].RootNode is not YamlSequenceNode sequence)
        {
            throw new Keystone.Core.Errors.KeystoneException(
                Keystone.Core.Errors.ErrorCode.ConfigValidationFailed,
                "entry config must be a top-level list");
        }

        // DC-8：引用环检测 visited 跨整次解析共享（跨条目/跨组都能检出环）
        var visited = interpolator is null ? null : new HashSet<string>(StringComparer.Ordinal);
        var entries = sequence.Children.Select(node => ParseEntry(node, interpolator, visited)).ToList();
        ValidateNoDuplicateIds(entries);
        return entries;
    }

    /// <summary>
    /// YAML 节点 → object 树（规则 0 手动转换；标量保持字符串形态，与 M.E.C 一致）。
    /// DC-8：config 子树标量携带 !!env/!!file tag 时经插值器展开。
    /// </summary>
    private static object? NodeToObject(YamlNode? node, StaticInterpolator? interpolator, HashSet<string>? visited)
    {
        switch (node)
        {
            case null:
                return null;

            case YamlScalarNode scalar:
                if (interpolator is not null && visited is not null
                    && !scalar.Tag.IsEmpty
                    && (string.Equals(scalar.Tag.Value, StaticInterpolator.EnvTag, StringComparison.Ordinal)
                        || string.Equals(scalar.Tag.Value, StaticInterpolator.FileTag, StringComparison.Ordinal)))
                {
                    return interpolator.InterpolateTagged(scalar.Tag.Value, scalar.Value ?? string.Empty, visited);
                }

                return scalar.Value;

            case YamlSequenceNode seq:
                return seq.Children.Select(child => NodeToObject(child, interpolator, visited)).ToList();

            case YamlMappingNode map:
                return map.Children.ToDictionary(
                    kv => (kv.Key as YamlScalarNode)?.Value ?? string.Empty,
                    kv => NodeToObject(kv.Value, interpolator, visited),
                    StringComparer.Ordinal);

            default:
                return null;
        }
    }

    private static EntryOptions ParseEntry(YamlNode node, StaticInterpolator? interpolator, HashSet<string>? visited)
    {
        if (node is not YamlMappingNode map)
        {
            throw new Keystone.Core.Errors.KeystoneException(
                Keystone.Core.Errors.ErrorCode.ConfigValidationFailed,
                "each entry must be a mapping");
        }

        return new EntryOptions
        {
            Id = Scalar(map, "id"),
            Name = Scalar(map, "name"),
            Config = NodeToObject(Get(map, "config"), interpolator, visited),
            Disabled = Bool(map, "disabled"),
            Insert = Bool(map, "insert") ?? false,
            Inject = StringList(map, "inject"),
            Isolate = StringList(map, "isolate").ToHashSet(StringComparer.Ordinal),
            Group = Get(map, "group") is YamlSequenceNode groupSequence
                ? groupSequence.Children.Select(child => ParseEntry(child, interpolator, visited)).ToList()
                : null,
        };
    }

    private static void ValidateNoDuplicateIds(IReadOnlyList<EntryOptions> entries)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry.Id is not null && !seen.Add(entry.Id))
            {
                throw new Keystone.Core.Errors.KeystoneException(
                    Keystone.Core.Errors.ErrorCode.ConfigValidationFailed,
                    $"duplicate entry id: {entry.Id}");
            }

            if (entry.Group is not null)
            {
                ValidateNoDuplicateIds(entry.Group);
            }
        }
    }

    private static YamlNode? Get(YamlMappingNode map, string key)
        => map.Children.FirstOrDefault(kv => string.Equals((kv.Key as YamlScalarNode)?.Value, key, StringComparison.Ordinal)).Value;

    private static string? Scalar(YamlMappingNode map, string key)
        => (Get(map, key) as YamlScalarNode)?.Value;

    private static bool? Bool(YamlMappingNode map, string key)
    {
        var value = Scalar(map, key);
        if (value is null)
        {
            return null;
        }

        return bool.Parse(value);
    }

    private static List<string> StringList(YamlMappingNode map, string key)
        => Get(map, key) is YamlSequenceNode sequence
            ? sequence.Children.OfType<YamlScalarNode>().Select(s => s.Value ?? string.Empty).ToList()
            : [];
}
