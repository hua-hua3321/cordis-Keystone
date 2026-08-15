namespace Keystone.Runtime.Events;

/// <summary>插件失败事实（DC-11，FAILED 转移：初始化失败/依赖超时；PluginRuntime 发布，尽力写）。</summary>
public sealed record PluginFailedFact(string PluginId, string? Reason) : IFactEvent
{
    public Guid TaskId => Guid.Empty;

    public string? Capability => null;

    public byte[]? Payload => null;

    public bool Durable => false;
}
