using Keystone.Core.Errors;

namespace Keystone.Runtime.Context;

/// <summary>服务存储实现：服务名 → (值, 属主)；属主校验 + rebind（03 §2.1/§2.3）。</summary>
public sealed class ServiceStore : IServiceStore
{
    private readonly Dictionary<string, Entry> _services = new(StringComparer.Ordinal);

    public void Set<T>(string serviceName, T value, string ownerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        if (_services.TryGetValue(serviceName, out var existing)
            && !string.Equals(existing.OwnerId, ownerId, StringComparison.Ordinal))
        {
            throw new KeystoneException(
                ErrorCode.ServiceAlreadyRegistered,
                $"service '{serviceName}' has been registered by '{existing.OwnerId}'");
        }

        _services[serviceName] = new Entry(value, ownerId);
    }

    public T? TryGet<T>(string serviceName)
        => _services.TryGetValue(serviceName, out var entry) ? (T)entry.Value : default;

    public T Get<T>(string serviceName)
        => TryGet<T>(serviceName)
            ?? throw new KeystoneException(ErrorCode.GatingServiceNotFound, $"service '{serviceName}' is not provided");

    private sealed record Entry(object Value, string OwnerId);
}
