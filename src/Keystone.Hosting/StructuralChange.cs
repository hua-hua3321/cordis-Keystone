using Keystone.Config.Entries;

namespace Keystone.Hosting;

/// <summary>结构变更条目 + 新树归属（P1-7，19 号审计 LD-17：Group 形状/归属入结构键）。</summary>
/// <param name="Entry">新条目（叶或组）。</param>
/// <param name="ParentId">新树父组 id（null = 根）。</param>
/// <param name="Position">新树位置（父组子列表/根列表下标）。</param>
public sealed record StructuralChange(EntryOptions Entry, string? ParentId, int? Position);
