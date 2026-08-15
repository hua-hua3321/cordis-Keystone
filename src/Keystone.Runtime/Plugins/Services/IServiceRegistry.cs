namespace Keystone.Runtime.Plugins.Services;

/// <summary>
/// 宿主级服务注册表（ADR-0007 决策 1：key = 服务名）：
/// 服务可用性 + 变更事件驱动依赖门控重新评估（internal/service 对应物）。
/// </summary>
public interface IServiceRegistry
{
    /// <summary>服务名当前是否可用（有提供者注册）。</summary>
    bool IsAvailable(string serviceName);

    /// <summary>当前可用服务名集合。</summary>
    IReadOnlySet<string> AvailableServices { get; }

    /// <summary>提供者注册服务（重复注册 = 幂等；变更时发事件）。</summary>
    void Register(string serviceName, string providerId);

    /// <summary>提供者注销服务（仅匹配 (服务名, 提供者) 才生效）。</summary>
    void Unregister(string serviceName, string providerId);

    /// <summary>订阅可用性变更（返回 disposer 退订）。</summary>
    IDisposable Subscribe(Action<ServiceAvailabilityChangedEventArgs> handler);
}
