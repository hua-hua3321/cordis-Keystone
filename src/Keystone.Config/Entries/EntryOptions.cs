namespace Keystone.Config.Entries;

/// <summary>
/// 配置条目（08 §3）：id/name/config/disabled/inject/isolate/group。
/// 对齐 Cordis loader 条目（含 F2 条目级 inject、F7 disabled 继承——父组挂起子树全挂）。
/// </summary>
public sealed record EntryOptions
{
    /// <summary>稳定标识（热更新 diff 依据）。</summary>
    public string? Id { get; init; }

    /// <summary>插件定位（源文件路径 / 内置 ID / 内建前缀）。</summary>
    public string? Name { get; init; }

    /// <summary>插件配置块（schema 校验后注入；object 树：Dictionary/List/标量）。</summary>
    public object? Config { get; init; }

    /// <summary>挂起不删（纯布尔，不支持表达式，ADR-0011）。</summary>
    public bool? Disabled { get; init; }

    /// <summary>条目级依赖声明（与 manifest inject 并集合并，F2）。</summary>
    public IReadOnlyList<string> Inject { get; init; } = [];

    /// <summary>组级服务隔离（03 §2.2）。</summary>
    public IReadOnlySet<string> Isolate { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>嵌套条目组（组级事务单元，F4）。</summary>
    public IReadOnlyList<EntryOptions>? Group { get; init; }

    /// <summary>true = 分层补丁中作为显式插入（未知 id 也插入；默认 false = 按 id 修改，未知跳过，F 系列 applyEntryPatches）。</summary>
    public bool Insert { get; init; }

    /// <summary>是否为组条目（组 config = 子条目列表，不套用条目级转换，F14）。</summary>
    public bool IsGroup => Group is not null;
}
