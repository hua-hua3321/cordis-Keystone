
namespace Keystone.Hosting;

/// <summary>条目卸载（对齐 loader/partial-dispose，F9）。</summary>
public sealed class EntryDisposingEventArgs : EventArgs
{
    public EntryDisposingEventArgs(string entryId, bool active)
    {
        EntryId = entryId;
        Active = active;
    }

    public string EntryId { get; }

    public bool Active { get; }
}
