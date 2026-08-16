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

        var oldById = Flatten(oldTree).ToDictionary(e => e.Entry.Id!, e => e, StringComparer.Ordinal);
        var newById = Flatten(newTree).ToDictionary(e => e.Entry.Id!, e => e, StringComparer.Ordinal);

        var added = newById.Values.Where(e => !oldById.ContainsKey(e.Entry.Id!))
            .Select(e =>
            {
                // P0-1（19 号审计 LD-1）：携带新树归属——扁平集不含谱系，宿主侧会插到根
                var (parent, position) = Locate(newTree, e.Entry.Id!);
                return new AddedEntry(e.Entry, parent, position);
            })
            .ToList();
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

            if (oldEntry.Entry.Disabled != newEntry.Entry.Disabled)
            {
                disabledFlips.Add(newEntry.Entry); // disabled 翻转优先（挂起/恢复路径）
                continue;
            }

            if (!string.Equals(StructuralKey(oldEntry), StructuralKey(newEntry), StringComparison.Ordinal))
            {
                structurallyChanged.Add(newEntry.Entry); // name/inject/isolate（生效域）变 → 冷重启
            }
            else if (!ConfigEquals(oldEntry.Entry.Config, newEntry.Entry.Config))
            {
                configChanged.Add(newEntry.Entry); // 仅 config 变 → 热更新
            }
        }

        return new ConfigDiff(added, removed, configChanged, structurallyChanged, disabledFlips);
    }

    /// <summary>扁平化（组递归展开——比对按叶/组条目 id 全集）。携带生效 isolate map（谱系累积，P57-T5）。</summary>
    private static IEnumerable<EffectiveEntry> Flatten(IReadOnlyList<EntryOptions> entries, Dictionary<string, string>? inherited = null)
    {
        foreach (var entry in entries)
        {
            var map = inherited is null ? [] : new Dictionary<string, string>(inherited, StringComparer.Ordinal);
            IsolateMapResolver.Apply(entry, map);
            yield return new EffectiveEntry(entry, map);
            if (entry.Group is { } children)
            {
                foreach (var child in Flatten(children, map))
                {
                    yield return child;
                }
            }
        }
    }

    /// <summary>结构键：冷重启判定字段（08 §6.1：name/inject/isolate 变 → 冷重启）。
    /// isolate 用生效 realm（#声明处Id/@label，谱系解析）——组级声明变化会改变叶子生效键 → 叶子冷重启（F10）。</summary>
    private static string StructuralKey(EffectiveEntry e)
        => $"{e.Entry.Name}|{string.Join(",", e.Entry.Inject)}|"
        + string.Join(",", e.EffectiveIsolate.OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value}"));

    private sealed record EffectiveEntry(EntryOptions Entry, Dictionary<string, string> EffectiveIsolate);

    /// <summary>P0-1：在树中定位条目归属（父组 id + 组内下标；根级 = (null, 根列表下标)）。</summary>
    private static (string? Parent, int? Position) Locate(IReadOnlyList<EntryOptions> tree, string id)
    {
        for (var i = 0; i < tree.Count; i++)
        {
            if (string.Equals(tree[i].Id, id, StringComparison.Ordinal))
            {
                return (null, i);
            }

            if (tree[i].Group is { } children)
            {
                for (var j = 0; j < children.Count; j++)
                {
                    if (string.Equals(children[j].Id, id, StringComparison.Ordinal))
                    {
                        return (tree[i].Id, j);
                    }
                }
            }
        }

        return (null, null); // 不可达（id 来自同树 Flatten）
    }

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
