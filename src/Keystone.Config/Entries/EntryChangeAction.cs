namespace Keystone.Config.Entries;

/// <summary>
/// 条目变更分级（F3，对齐 Cordis loader entry.update diff 语义）：
/// 无变化不动；name/inject/group 变 → 冷重启；仅 config 变 → 热更新；disabled 翻转 → 仅卸载。
/// </summary>
public enum EntryChangeAction
{
    None,
    HotUpdate,
    Restart,
    DisposeOnly,
}

