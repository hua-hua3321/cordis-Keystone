namespace Keystone.Config.Entries;

/// <summary>
/// 运行期 patch 应用（CA-5，P61，对齐 Cordis include/index.ts Config.patches 读后插入）：
/// 纯函数——插入组（GroupId 非空）或根；覆盖按 id 合并非 null 字段；name 不匹配跳过 + 可选警告回调。
/// 应用时机：宿主 StartAsync 解析后、manifest 校验前（patch 后的树才进校验——对齐 Cordis patch 在 schema 前生效）。
/// </summary>
public static class EntryPatcher
{
    /// <summary>应用 patch 列表（顺序应用；空列表 = 恒等返回原引用）。</summary>
    public static IReadOnlyList<EntryOptions> Apply(
        IReadOnlyList<EntryOptions> tree,
        IReadOnlyList<EntryPatch> patches,
        Action<string>? onWarn = null)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(patches);
        if (patches.Count == 0)
        {
            return tree; // 恒等（同引用）
        }

        var entries = tree.ToList(); // 拷贝应用（不变异入参）
        foreach (var patch in patches)
        {
            ApplyInsert(entries, patch, onWarn);
            ApplyOverrides(entries, patch, onWarn);
        }

        return entries;
    }

    private static void ApplyInsert(List<EntryOptions> entries, EntryPatch patch, Action<string>? onWarn)
    {
        if (patch.Insert is not { Count: > 0 } insert)
        {
            return;
        }

        if (patch.GroupId is null)
        {
            entries.AddRange(insert); // 插入根
            return;
        }

        var index = entries.FindIndex(e => string.Equals(e.Id, patch.GroupId, StringComparison.Ordinal));
        if (index < 0)
        {
            onWarn?.Invoke($"patch group not found: {patch.GroupId}"); // 跳过 + 警告（对齐 name 不匹配跳过）
            return;
        }

        var group = entries[index];
        if (group.Group is null)
        {
            onWarn?.Invoke($"patch target is not a group: {patch.GroupId}");
            return;
        }

        entries[index] = group with { Group = [.. group.Group, .. insert] };
    }

    private static void ApplyOverrides(List<EntryOptions> entries, EntryPatch patch, Action<string>? onWarn)
    {
        if (patch.Overrides is not { Count: > 0 } overrides)
        {
            return;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.Id is not null && overrides.TryGetValue(entry.Id, out var patchEntry))
            {
                if (!string.Equals(patchEntry.Name, entry.Name, StringComparison.Ordinal)
                    && patchEntry.Name is not null)
                {
                    onWarn?.Invoke($"patch name mismatch for id {entry.Id}: {patchEntry.Name} != {entry.Name}");
                    continue; // name 不匹配跳过（对齐 Cordis 按 name 匹配）
                }

                entries[i] = MergeEntry(entry, patchEntry);
            }

            if (entry.Group is { } children) // 递归组内覆盖
            {
                var childList = children.ToList();
                var childChanged = false;
                for (var c = 0; c < childList.Count; c++)
                {
                    var child = childList[c];
                    if (child.Id is not null && overrides.TryGetValue(child.Id, out var childPatch))
                    {
                        if (childPatch.Name is not null
                            && !string.Equals(childPatch.Name, child.Name, StringComparison.Ordinal))
                        {
                            onWarn?.Invoke($"patch name mismatch for id {child.Id}");
                            continue;
                        }

                        childList[c] = MergeEntry(child, childPatch);
                        childChanged = true;
                    }
                }

                if (childChanged)
                {
                    entries[i] = entry with { Group = childList };
                }
            }
        }
    }

    /// <summary>非 null 字段合并（patch 提供 → 覆盖；null → 保留原值）。</summary>
    private static EntryOptions MergeEntry(EntryOptions entry, EntryOptions patch) => new()
    {
        Id = patch.Id ?? entry.Id,
        Name = patch.Name ?? entry.Name,
        Config = patch.Config ?? entry.Config,
        Inject = patch.Inject ?? entry.Inject,
        Isolate = patch.Isolate.Count > 0 ? patch.Isolate : entry.Isolate,
        Disabled = patch.Disabled ?? entry.Disabled,
        Insert = patch.Insert || entry.Insert,
        Group = patch.Group ?? entry.Group,
    };
}
