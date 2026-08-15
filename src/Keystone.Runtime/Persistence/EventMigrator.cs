namespace Keystone.Runtime.Persistence;

/// <summary>
/// 事件格式迁移（ADR-0009 风险表：SchemaVersion）：读取时把旧版本事件逐级迁移到最新格式。
/// migrations 键 = 源版本，值 = 迁移函数（返回的 SchemaVersion 决定下一级）。
/// </summary>
public sealed class EventMigrator
{
    private readonly IReadOnlyDictionary<int, Func<StoredFact, StoredFact>> _migrations;

    public EventMigrator(IReadOnlyDictionary<int, Func<StoredFact, StoredFact>> migrations)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        _migrations = migrations;
    }

    /// <summary>按 SchemaVersion 逐级迁移到最新（无迁移 = 透传）。</summary>
    public StoredFact Migrate(StoredFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);

        var current = fact;
        var visited = new HashSet<int> { current.SchemaVersion };
        while (_migrations.TryGetValue(current.SchemaVersion, out var migrate))
        {
            current = migrate(current);
            if (!visited.Add(current.SchemaVersion))
            {
                break; // 迁移环防御
            }
        }

        return current;
    }
}
