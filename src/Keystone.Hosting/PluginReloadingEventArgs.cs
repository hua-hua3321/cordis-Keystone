namespace Keystone.Hosting;

/// <summary>条目冷重启事件（DC-9，08 §6.1 name/inject/isolate 变路径）。</summary>
public sealed class PluginReloadingEventArgs(string entryId) : EventArgs
{
    public string EntryId { get; } = entryId;
}
