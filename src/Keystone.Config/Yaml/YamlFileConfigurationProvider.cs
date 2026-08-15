using System.Globalization;
using Microsoft.Extensions.Configuration;
using YamlDotNet.RepresentationModel;

namespace Keystone.Config.Yaml;

/// <summary>
/// YAML configuration provider: parses a YAML document and flattens it into the
/// configuration key space (mappings/sequences → ':'-delimited keys, scalars → strings,
/// nulls and empty containers skipped per M.E.C. convention).
///
/// AOT 说明（规则 0）：解析走 <see cref="YamlStream"/> 节点树（YamlNode 遍历），纯解析无反射，
/// 不触碰 YamlDotNet 的反射反序列化器（<c>Deserializer</c>）——后者在 AOT 下触发 IL3050。
/// 别名（anchor/alias）与 merge keys（&lt;&lt;）在本提供者内手动解析。
/// </summary>
public sealed class YamlFileConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly YamlFileConfigurationSource _source;
    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;
    private bool _disposed;

    public YamlFileConfigurationProvider(YamlFileConfigurationSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrWhiteSpace(source.Path))
        {
            throw new ArgumentException("YAML file path must not be empty.", nameof(source));
        }

        _source = source;
    }

    public override void Load()
    {
        var text = ReadFile();
        if (text is null)
        {
            Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            OnReload();
            return;
        }

        var stream = new YamlStream();
        stream.Load(new StringReader(text));
        var root = stream.Documents.Count > 0 ? stream.Documents[0].RootNode : null;

        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        Flatten(root, string.Empty, data);
        Data = data;
        OnReload();
        StartWatching();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _debounceTimer?.Dispose();
        _watcher?.Dispose();
        GC.SuppressFinalize(this);
    }

    private string? ReadFile()
    {
        if (!File.Exists(_source.Path))
        {
            if (_source.Optional)
            {
                return null;
            }

            throw new FileNotFoundException($"YAML configuration file not found: {_source.Path}", _source.Path);
        }

        return File.ReadAllText(_source.Path);
    }

    private static void Flatten(YamlNode? node, string prefix, IDictionary<string, string?> data)
    {
        if (node is null)
        {
            return;
        }

        switch (node)
        {
            case YamlMappingNode map:
                foreach (var (keyNode, valueNode) in EffectiveEntries(map))
                {
                    var keyPart = (keyNode as YamlScalarNode)?.Value;
                    if (string.IsNullOrEmpty(keyPart))
                    {
                        continue;
                    }

                    Flatten(valueNode, prefix.Length == 0 ? keyPart : prefix + ConfigurationPath.KeyDelimiter + keyPart, data);
                }

                break;

            case YamlSequenceNode seq:
                for (var i = 0; i < seq.Children.Count; i++)
                {
                    Flatten(seq.Children[i], prefix + ConfigurationPath.KeyDelimiter + i.ToString(CultureInfo.InvariantCulture), data);
                }

                break;

            case YamlScalarNode scalar when scalar.Value is not null:
                data[prefix] = scalar.Value; // 标量保持字符串形态（M.E.C 值均为字符串）；null/空标量跳过
                break;
        }
    }

    /// <summary>
    /// Build the effective entries of a mapping: YAML merge keys (<c>&lt;&lt;</c>) are resolved
    /// (single mapping or a sequence of mappings) and merged first, local keys override merged
    /// ones. Aliases are resolved by <see cref="YamlStream"/> during parsing.
    /// </summary>
    private static IEnumerable<KeyValuePair<YamlNode, YamlNode>> EffectiveEntries(YamlMappingNode map)
    {
        var hasMergeKey = map.Children.Any(pair => string.Equals((pair.Key as YamlScalarNode)?.Value, "<<", StringComparison.Ordinal));
        if (!hasMergeKey)
        {
            return map.Children;
        }

        var merged = new List<KeyValuePair<YamlNode, YamlNode>>();
        var seen = new HashSet<string?>(StringComparer.Ordinal);
        foreach (var (keyNode, valueNode) in map.Children)
        {
            if (string.Equals((keyNode as YamlScalarNode)?.Value, "<<", StringComparison.Ordinal))
            {
                foreach (var source in EnumerateMergeSources(valueNode))
                {
                    foreach (var (mergeKey, mergeValue) in source)
                    {
                        var key = (mergeKey as YamlScalarNode)?.Value;
                        if (key is not null && seen.Add(key))
                        {
                            merged.Add(new KeyValuePair<YamlNode, YamlNode>(mergeKey, mergeValue));
                        }
                    }
                }

                continue;
            }

            var localKey = (keyNode as YamlScalarNode)?.Value;
            if (localKey is not null)
            {
                merged.RemoveAll(pair => string.Equals((pair.Key as YamlScalarNode)?.Value, localKey, StringComparison.Ordinal)); // 本地覆盖
            }

            merged.Add(new KeyValuePair<YamlNode, YamlNode>(keyNode, valueNode));
        }

        return merged;
    }

    private static IEnumerable<IEnumerable<KeyValuePair<YamlNode, YamlNode>>> EnumerateMergeSources(YamlNode mergeNode)
    {
        switch (mergeNode)
        {
            case YamlMappingNode map:
                yield return map.Children;
                break;

            case YamlSequenceNode seq:
                foreach (var child in seq.Children)
                {
                    if (child is YamlMappingNode childMap)
                    {
                        yield return childMap.Children;
                    }
                }

                break;
        }
    }

    private void StartWatching()
    {
        if (!_source.ReloadOnChange || _watcher is not null || _disposed)
        {
            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(_source.Path));
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        _watcher = new FileSystemWatcher(directory, Path.GetFileName(_source.Path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += (_, _) => ScheduleReload();
        _watcher.Created += (_, _) => ScheduleReload();
        _watcher.Deleted += (_, _) => ScheduleReload();
        _watcher.Renamed += (_, _) => ScheduleReload();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031",
        Justification = "文件瞬时不可读（写入中途）时保持旧数据，等下一个变更事件（'最后好数据保持'，doc 08 §6.3）")]
    private void ScheduleReload()
    {
        if (_disposed)
        {
            return;
        }

        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(
            _ =>
            {
                try
                {
                    if (!_disposed)
                    {
                        Load(); // 重新读取并 OnReload，配置变更走原子替换语义（doc 08 §6）
                    }
                }
                catch (Exception)
                {
                    // 文件瞬时不可读（写入中途）：保持旧数据，等下一个变更事件（"最后好数据保持"）
                }
            },
            null,
            _source.ReloadDelay,
            Timeout.InfiniteTimeSpan);
    }
}
