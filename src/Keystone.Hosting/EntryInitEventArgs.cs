using Keystone.Config.Entries;

namespace Keystone.Hosting;

/// <summary>条目创建（对齐 loader/entry-init，F9）。</summary>
public sealed class EntryInitEventArgs : EventArgs
{
    public EntryInitEventArgs(EntryOptions entry)
    {
        Entry = entry;
    }

    public EntryOptions Entry { get; }
}
