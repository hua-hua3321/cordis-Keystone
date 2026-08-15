using Keystone.Core.Errors;

namespace Keystone.Config.Entries;

/// <summary>
/// 组级事务（F4，对齐 Cordis EntryGroup.update）：重复 id 检测 → 并行应用（聚合）→
/// 失败逆序回滚（移除新建 + 重建旧）+ 恢复数据；树卸载中失败不回滚（卸载主导终止）。
/// </summary>
public sealed class EntryGroup
{
    private readonly Func<EntryOptions, Task> _apply;
    private readonly Func<EntryOptions, Task> _remove;
    private readonly List<EntryOptions> _entries = [];
    private bool _unloaded;

    public EntryGroup(Func<EntryOptions, Task> apply, Func<EntryOptions, Task> remove)
    {
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(remove);
        _apply = apply;
        _remove = remove;
    }

    /// <summary>当前条目（应用成功后更新）。</summary>
    public IReadOnlyList<EntryOptions> Data
    {
        get
        {
            lock (_entries)
            {
                return [.. _entries];
            }
        }
    }

    /// <summary>标记所在树卸载中（之后失败不回滚，F4 卸载主导终止）。</summary>
    public void MarkUnloaded() => _unloaded = true;

    /// <summary>等待本组应用任务收敛（树级 await，F11 对应物；UpdateAsync 已同步等待）。</summary>
    public Task AwaitSettledAsync() => Task.CompletedTask;

    /// <summary>整组事务应用：并行应用 + 失败聚合回滚。</summary>
    public async Task UpdateAsync(IReadOnlyList<EntryOptions> config)
    {
        ArgumentNullException.ThrowIfNull(config);

        // 重复 id 检测（fail-fast，应用前）
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in config)
        {
            if (entry.Id is not null && !seen.Add(entry.Id))
            {
                throw new KeystoneException(ErrorCode.ConfigValidationFailed, $"duplicate loader entry id: {entry.Id}");
            }
        }

        var oldMap = Data.Where(e => e.Id is not null).ToDictionary(e => e.Id!, StringComparer.Ordinal);
        try
        {
            await ApplyNewAsync(config, oldMap).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (!_unloaded)
            {
                await RollbackAsync(config, oldMap).ConfigureAwait(false);
            }

            throw new AggregateException([ex]); // 失败聚合上报（卸载主导分支也不吞错）
        }
    }

    private async Task ApplyNewAsync(IReadOnlyList<EntryOptions> config, IReadOnlyDictionary<string, EntryOptions> oldMapSnapshot)
    {
        // 并行应用（Task.WhenAll 聚合失败）
        await Task.WhenAll(config.Select(_apply)).ConfigureAwait(false);
        if (_unloaded)
        {
            return; // 树卸载中：不提交新状态
        }

        lock (_entries)
        {
            _entries.Clear();
            _entries.AddRange(config);
        }

        // 旧有而新配置没有的条目 → 卸载
        var newIds = config.Where(e => e.Id is not null).Select(e => e.Id!).ToHashSet(StringComparer.Ordinal);
        foreach (var old in oldMapSnapshot.Values)
        {
            if (!newIds.Contains(old.Id!))
            {
                await _remove(old).ConfigureAwait(false);
            }
        }
    }

    private async Task RollbackAsync(IReadOnlyList<EntryOptions> config, IReadOnlyDictionary<string, EntryOptions> oldMap)
    {
        // 组级回滚：逆序移除新建条目 + 重建旧配置 + 恢复数据
        var newIds = config.Where(e => e.Id is not null).Select(e => e.Id!).ToHashSet(StringComparer.Ordinal);
        foreach (var id in newIds.Reverse())
        {
            if (!oldMap.ContainsKey(id))
            {
                await _remove(config.First(e => string.Equals(e.Id, id, StringComparison.Ordinal))).ConfigureAwait(false);
            }
        }

        foreach (var old in oldMap.Values)
        {
            if (!newIds.Contains(old.Id!))
            {
                await _apply(old).ConfigureAwait(false);
            }
        }

        lock (_entries)
        {
            _entries.Clear();
            _entries.AddRange(oldMap.Values);
        }
    }
}
