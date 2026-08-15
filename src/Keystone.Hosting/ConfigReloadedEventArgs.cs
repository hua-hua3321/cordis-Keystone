namespace Keystone.Hosting;

/// <summary>配置重载完成事件（DC-9，08 §6）：负载 = 变更条目 id 集。</summary>
public sealed class ConfigReloadedEventArgs(IReadOnlyList<string> changedIds) : EventArgs
{
    public IReadOnlyList<string> ChangedIds { get; } = changedIds;
}
