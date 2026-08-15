using Keystone.Config.Entries;

namespace Keystone.Hosting;

/// <summary>
/// 配置树比对（DC-9，08 §6.1 变更分级）：按条目 id 对齐；字段级别决定动作——
/// 仅 config 变 = 热更新；name/inject/isolate 变 = 冷重启；disabled 翻转 = 挂起路径；
/// 相等即跳过（deepEqual 语义）。
/// </summary>
public static class ConfigDiffer
{
    public static ConfigDiff Diff(IReadOnlyList<EntryOptions> oldTree, IReadOnlyList<EntryOptions> newTree)
    {
        ArgumentNullException.ThrowIfNull(oldTree);
        ArgumentNullException.ThrowIfNull(newTree);

        var oldById = Flatten(oldTree).ToDictionary(e => e.Id!, e => e, StringComparer.Ordinal);
        var newById = Flatten(newTree).ToDictionary(e => e.Id!, e => e, StringComparer.Ordinal);

        var added = newById.Values.Where(e => !oldById.ContainsKey(e.Id!)).ToList();
        var removed = oldById.Keys.Where(id => !newById.ContainsKey(id)).ToList();

        var configChanged = new List<EntryOptions>();
        var structurallyChanged = new List<EntryOptions>();
        var disabledFlips = new List<EntryOptions>();
        foreach (var (id, newEntry) in newById)
        {
            if (!oldById.TryGetValue(id, out var oldEntry))
            {
                continue; // 新增已归 added
            }

            if (oldEntry.Disabled != newEntry.Disabled)
            {
                disabledFlips.Add(newEntry); // disabled 翻转优先（挂起/恢复路径）
                continue;
            }

            if (!string.Equals(StructuralKey(oldEntry), StructuralKey(newEntry), StringComparison.Ordinal))
            {
                structurallyChanged.Add(newEntry); // name/inject/isolate 变 → 冷重启
            }
            else if (!ConfigEquals(oldEntry.Config, newEntry.Config))
            {
                configChanged.Add(newEntry); // 仅 config 变 → 热更新
            }
        }

        return new ConfigDiff(added, removed, configChanged, structurallyChanged, disabledFlips);
    }

    /// <summary>扁平化（组递归展开——比对按叶/组条目 id 全集）。</summary>
    private static IEnumerable<EntryOptions> Flatten(IReadOnlyList<EntryOptions> entries)
    {
        foreach (var entry in entries)
        {
            yield return entry;
            if (entry.Group is { } children)
            {
                foreach (var child in Flatten(children))
                {
                    yield return child;
                }
            }
        }
    }

    /// <summary>结构键：冷重启判定的字段（08 §6.1：name/inject/group 变 → 冷重启）。</summary>
    private static string StructuralKey(EntryOptions e)
        => $"{e.Name}|{string.Join(",", e.Inject)}|{string.Join(",", e.Isolate)}";

    /// <summary>config 比对（引用相等短路；字典值逐键比——YAML 重读必是新实例）。</summary>
    private static bool ConfigEquals(object? oldConfig, object? newConfig)
    {
        if (ReferenceEquals(oldConfig, newConfig))
        {
            return true;
        }

        if (oldConfig is null || newConfig is null)
        {
            return oldConfig is null && newConfig is null;
        }

        if (oldConfig is Dictionary<string, object?> oldMap
            && newConfig is Dictionary<string, object?> newMap)
        {
            if (oldMap.Count != newMap.Count)
            {
                return false;
            }

            foreach (var (key, value) in oldMap)
            {
                if (!newMap.TryGetValue(key, out var other) || !Equals(value, other))
                {
                    return false;
                }
            }

            return true;
        }

        return Equals(oldConfig, newConfig);
    }
}
