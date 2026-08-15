using Keystone.Config.Entries;

namespace Keystone.Hosting;

/// <summary>条目上下文补丁（对齐 loader/patch-context waterfall，F9；isolate 变更在此接线，03 §2.2）。</summary>
public sealed class PatchContextEventArgs : EventArgs
{
    public PatchContextEventArgs(EntryOptions entry)
    {
        Entry = entry;
    }

    public EntryOptions Entry { get; }
}
