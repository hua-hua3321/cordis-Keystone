using Keystone.Config.Entries;

namespace Keystone.Hosting;

/// <summary>配置写回前通知（对齐 loader/config-update，F9）。</summary>
public sealed class ConfigUpdateEventArgs : EventArgs
{
    public ConfigUpdateEventArgs(IReadOnlyList<EntryOptions> entries)
    {
        Entries = entries;
    }

    public IReadOnlyList<EntryOptions> Entries { get; }
}
