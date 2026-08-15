using Keystone.Core.Errors;

namespace Keystone.Config.Entries;

/// <summary>
/// 分层叠加（08 §4）：空列表 → base → profile → 用户 patch → 运行期 overlay，按序合并。
/// patch 语义（对齐 include applyEntryPatches）：按 id 合并（patch 提供的字段覆盖，其余保留）；
/// 无 id 条目跳过；层内重复 id = 配置错误 fail-fast。
/// </summary>
public static class EntryTree
{
    public static IReadOnlyList<EntryOptions> ApplyLayers(IReadOnlyList<IReadOnlyList<EntryOptions>> layers)
    {
        ArgumentNullException.ThrowIfNull(layers);

        var merged = new List<EntryOptions>();
        for (var layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            var layer = layers[layerIndex];
            var isBase = layerIndex == 0;
            var layerIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var candidate in layer)
            {
                if (candidate.Id is null)
                {
                    continue; // 无 id 条目不参与分层
                }

                // 层内重复 id fail-fast
                if (!layerIds.Add(candidate.Id))
                {
                    throw new KeystoneException(
                        ErrorCode.ConfigValidationFailed,
                        $"duplicate entry id in layer: {candidate.Id}");
                }

                var index = merged.FindIndex(e => string.Equals(e.Id, candidate.Id, StringComparison.Ordinal));
                if (index < 0)
                {
                    if (isBase || candidate.Insert)
                    {
                        merged.Add(candidate); // base 层全插入；patch 层显式 insert 才插入
                    }

                    continue; // patch 非 insert 且未知 id → 跳过（F 系列 patch 语义）
                }

                // 补丁合并：candidate 提供的字段覆盖；未提供的保留底层值
                var existing = merged[index];
                merged[index] = existing with
                {
                    Name = candidate.Name ?? existing.Name,
                    Config = candidate.Config ?? existing.Config,
                    Disabled = candidate.Disabled ?? existing.Disabled,
                    Inject = candidate.Inject.Count > 0 ? candidate.Inject : existing.Inject,
                    Isolate = candidate.Isolate.Count > 0 ? candidate.Isolate : existing.Isolate,
                    Group = candidate.Group ?? existing.Group,
                };
            }
        }

        return merged;
    }
}
