using Keystone.Runtime.Context;

namespace Keystone.Runtime.Plugins.Services;

/// <summary>
/// 发现层内存投影（P57-T4）：直接投影 <see cref="KeyedServiceStore"/>——零冗余状态（不持有独立 availability）。
/// 未来 Redis/Consul adapter 实现同接口（远端 watch → 本地缓存同步），PluginRuntime/值层零改动。
/// </summary>
public sealed class InMemoryServiceDiscovery : IServiceDiscovery
{
    private readonly KeyedServiceStore _store;

    public InMemoryServiceDiscovery(KeyedServiceStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public bool IsAvailable(string serviceName, string realm) => _store.IsAvailable(serviceName, realm);

    public IReadOnlyList<string> AvailableServices(string realm) => _store.AvailableServices(realm);

    public IDisposable Subscribe(Action<IReadOnlyList<ServiceKey>> handler) => _store.Subscribe(handler);
}
