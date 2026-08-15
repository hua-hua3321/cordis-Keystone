using Keystone.Core.Errors;

namespace Keystone.Runtime.Plugins.Services;

/// <summary>服务注册表实现（线程安全；事件驱动门控重评）。</summary>
public sealed class ServiceRegistry : IServiceRegistry
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, string> _providers = new(StringComparer.Ordinal);

    public event EventHandler<ServiceAvailabilityChangedEventArgs>? ServiceAvailabilityChanged;

    public IReadOnlySet<string> AvailableServices
    {
        get
        {
            lock (_lock)
            {
                return _providers.Keys.ToHashSet(StringComparer.Ordinal);
            }
        }
    }

    public bool IsAvailable(string serviceName)
    {
        lock (_lock)
        {
            return _providers.ContainsKey(serviceName);
        }
    }

    public void Register(string serviceName, string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);

        var changed = false;
        lock (_lock)
        {
            // DC-6（02 §3 / ADR-0007）：同 scope 重复注册 = 报错（rebind 语义，禁止静默覆盖）
            if (_providers.TryGetValue(serviceName, out var existing)
                && !string.Equals(existing, providerId, StringComparison.Ordinal))
            {
                throw new KeystoneException(
                    ErrorCode.ServiceAlreadyRegistered,
                    $"service '{serviceName}' has been registered by '{existing}'");
            }

            changed = _providers.TryAdd(serviceName, providerId);
        }

        if (changed)
        {
            ServiceAvailabilityChanged?.Invoke(this, new ServiceAvailabilityChangedEventArgs(serviceName, available: true));
        }
    }

    public void Unregister(string serviceName, string providerId)
    {
        var changed = false;
        lock (_lock)
        {
            if (_providers.TryGetValue(serviceName, out var owner)
                && string.Equals(owner, providerId, StringComparison.Ordinal))
            {
                _providers.Remove(serviceName);
                changed = true;
            }
        }

        if (changed)
        {
            ServiceAvailabilityChanged?.Invoke(this, new ServiceAvailabilityChangedEventArgs(serviceName, available: false));
        }
    }

    public IDisposable Subscribe(Action<ServiceAvailabilityChangedEventArgs> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        EventHandler<ServiceAvailabilityChangedEventArgs> wrapper = (_, args) => handler(args);
        ServiceAvailabilityChanged += wrapper;
        return new Subscription(() => ServiceAvailabilityChanged -= wrapper);
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;
        private bool _disposed;

        public Subscription(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _unsubscribe();
            GC.SuppressFinalize(this);
        }
    }
}
