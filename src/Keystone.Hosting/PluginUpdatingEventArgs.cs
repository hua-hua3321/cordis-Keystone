namespace Keystone.Hosting;

/// <summary>条目热更新事件（DC-9，08 §6.1 仅 config 变路径）。</summary>
public sealed class PluginUpdatingEventArgs(string entryId, object? newConfig) : EventArgs
{
    public string EntryId { get; } = entryId;

    public object? NewConfig { get; } = newConfig;
}
