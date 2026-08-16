namespace Keystone.Config.Entries;

/// <summary>
/// 运行期 patch 描述（CA-5，P61，对齐 Cordis include Config.patches）：读后插入（组/根）+ 按 id 覆盖。
/// 注意与 PatchContextAsync（上下文补丁瀑布，F9）语义不同——本类型只作用于条目树结构。
/// </summary>
/// <param name="GroupId">插入目标组 id；null = 根。组不存在 → 跳过 + onWarn。</param>
/// <param name="Insert">插入的条目列表（组尾/根尾追加）。</param>
/// <param name="Overrides">按 id 覆盖：条目 id → 覆盖值（非 null 字段合并；name 不匹配跳过）。</param>
public sealed record EntryPatch(
    string? GroupId,
    IReadOnlyList<EntryOptions>? Insert,
    IReadOnlyDictionary<string, EntryOptions>? Overrides);
