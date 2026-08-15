namespace Keystone.Runtime.Plugins.Services;

/// <summary>服务可用性变更通知（ADR-0007 决策 3：服务提供方注册/卸载时发布）。</summary>
public sealed class ServiceAvailabilityChangedEventArgs : EventArgs
{
    public ServiceAvailabilityChangedEventArgs(string serviceName, bool available)
    {
        ServiceName = serviceName;
        Available = available;
    }

    public string ServiceName { get; }

    public bool Available { get; }
}
