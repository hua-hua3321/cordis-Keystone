namespace Keystone.Config.Entries;

public static class EntryChangeClassifier
{
    public static EntryChangeAction Classify(EntryOptions previous, EntryOptions candidate)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(candidate);

        if (!string.Equals(previous.Name, candidate.Name, StringComparison.Ordinal)
            || !previous.Inject.SequenceEqual(candidate.Inject, StringComparer.Ordinal)
            || !Equals(previous.Group, candidate.Group))
        {
            return EntryChangeAction.Restart; // name/inject/group 变 → 冷重启
        }

        if (previous.Disabled != candidate.Disabled)
        {
            return EntryChangeAction.DisposeOnly; // disabled 翻转 → 仅卸载
        }

        if (!Equals(previous.Config, candidate.Config))
        {
            return EntryChangeAction.HotUpdate; // 仅 config 变 → 热更新
        }

        return EntryChangeAction.None;
    }
}
