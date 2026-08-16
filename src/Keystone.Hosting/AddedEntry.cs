using Keystone.Config.Entries;

namespace Keystone.Hosting;

/// <summary>新增条目 + 新树归属（P0-1，19 号审计 LD-1：组谱系随 diff 携带，否则子叶被插到根）。</summary>
/// <param name="Entry">新增条目（组或叶）。</param>
/// <param name="ParentId">新树父组 id（null = 根）。</param>
/// <param name="Position">组内下标（父组子列表的精确位置）。</param>
public sealed record AddedEntry(EntryOptions Entry, string? ParentId, int? Position);
