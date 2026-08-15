using Keystone.Config.Entries;

namespace Keystone.Hosting;

/// <summary>配置树 diff 结果（DC-9，08 §6.1）：按条目 id 比对。</summary>
/// <param name="Added">新树有旧树无（→ 加载）。</param>
/// <param name="Removed">旧树有新树无（→ 卸载）。</param>
/// <param name="ConfigChanged">仅 config 变（→ 热更新）。</param>
/// <param name="StructurallyChanged">name/inject/group 变（→ 冷重启）。</param>
/// <param name="DisabledFlips">disabled 翻转（→ 挂起/恢复）。</param>
public sealed record ConfigDiff(
    IReadOnlyList<EntryOptions> Added,
    IReadOnlyList<string> Removed,
    IReadOnlyList<EntryOptions> ConfigChanged,
    IReadOnlyList<EntryOptions> StructurallyChanged,
    IReadOnlyList<EntryOptions> DisabledFlips)
{
    /// <summary>全部变更条目 id（诊断/事件负载）。</summary>
    public IReadOnlyList<string> ChangedIds =>
    [
        .. Added.Select(e => e.Id!),
        .. Removed,
        .. ConfigChanged.Select(e => e.Id!),
        .. StructurallyChanged.Select(e => e.Id!),
        .. DisabledFlips.Select(e => e.Id!),
    ];

    /// <summary>无变更。</summary>
    public bool IsEmpty =>
        Added.Count == 0 && Removed.Count == 0 && ConfigChanged.Count == 0
        && StructurallyChanged.Count == 0 && DisabledFlips.Count == 0;
}
