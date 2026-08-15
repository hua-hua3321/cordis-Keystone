namespace Keystone.Runtime.Events;

/// <summary>插件启动事实（DC-11，ACTIVE 转移；PluginRuntime 发布，尽力写）。</summary>
public sealed record PluginStartedFact(string PluginId) : IFactEvent
{
    public Guid TaskId => Guid.Empty;

    public string? Capability => null;

    public byte[]? Payload => null;

    public bool Durable => false;
}
