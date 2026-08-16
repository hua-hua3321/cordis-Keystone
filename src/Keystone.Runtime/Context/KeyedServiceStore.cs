using System.Collections.Concurrent;
using Keystone.Core.Errors;

namespace Keystone.Runtime.Context;

/// <summary>
/// 键控服务值存储（18 §2 CA-1 第 1 步，P57-T2）：值层唯一事实源——
/// (服务名, realm) → (值, 属主)；<see cref="ConcurrentDictionary{TKey,TValue}"/> 热路径无锁读 +
/// <see cref="Lock"/> 复合写（属主校验 + 写原子，G14 防 rebind TOCTOU）。
/// 变更通知<b>出锁后</b>触发（回调内可再查本 store，跨线程不死锁）；
/// scope 批量合并（对齐 Cordis <c>notify(names[])</c>：init 期 N 个 provide 合并一次唤醒）；
/// 可用 = ContainsKey（消灭"门控放行但 Get 落空"）。
/// 订阅者抛异常会中断本轮后续投递并向触发方冒泡（与现行 ServiceRegistry 事件多播行为一致）。
/// </summary>
public sealed class KeyedServiceStore
{
    private readonly ConcurrentDictionary<ServiceKey, Entry> _services = new();
    private readonly Lock _lock = new();

    // 订阅者（copy-on-write：订阅/退订锁内换列表；触发前锁内取快照、锁外遍历）
    private List<Action<IReadOnlyList<ServiceKey>>> _handlers = [];

    // 活动通知 scope 栈顶（锁内维护；dispose 弹出，栈空后出锁统一发累积变更）
    private NotifyScope? _activeScope;

    /// <summary>开启批量通知 scope：scope 内的 provide/remove 变更累积，dispose 时合并为一次通知（嵌套并入最外层）。</summary>
    public IDisposable BeginNotifyScope()
    {
        lock (_lock)
        {
            var scope = new NotifyScope(this, _activeScope);
            _activeScope = scope;
            return scope;
        }
    }

    /// <summary>
    /// 注册服务值（值即注册）：写键并返回删键 disposer（dispose = 属主移除 + Removed 通知；已移除则幂等无操作）。
    /// 同键同属主 = rebind（G14 允许，值更新）；同键异属主 = ServiceAlreadyRegistered。
    /// </summary>
    public IDisposable Provide<T>(string serviceName, string realm, T value, string ownerId)
    {
        var key = ValidateKey(serviceName, realm);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        var immediate = WriteWithOwnerCheck(key, value, ownerId);
        NotifyIfPresent(immediate);
        return new Disposer(this, key, ownerId);
    }

    /// <summary>读值（无锁热路径）：缺失返回 default。</summary>
    public T? TryGet<T>(string serviceName, string realm)
    {
        ValidateKey(serviceName, realm);
        return _services.TryGetValue(new ServiceKey(serviceName, realm), out var entry) ? (T)entry.Value : default;
    }

    /// <summary>读值：缺失抛 <see cref="ErrorCode.GatingServiceNotFound"/>。</summary>
    public T Get<T>(string serviceName, string realm)
        => TryGet<T>(serviceName, realm)
            ?? throw new KeystoneException(
                ErrorCode.GatingServiceNotFound,
                $"service '{serviceName}' (realm '{realm}') is not provided");

    /// <summary>可用 = 值存在（无锁热路径，单一事实源）。</summary>
    public bool IsAvailable(string serviceName, string realm)
    {
        ValidateKey(serviceName, realm);
        return _services.ContainsKey(new ServiceKey(serviceName, realm));
    }

    /// <summary>移除服务值（属主校验）：键缺失 = 幂等无操作（不发通知）。</summary>
    public void Remove(string serviceName, string realm, string ownerId)
    {
        var key = ValidateKey(serviceName, realm);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        var immediate = RemoveWithOwnerCheck(key, ownerId);
        NotifyIfPresent(immediate);
    }

    /// <summary>指定域内已注册服务名（诊断/发现投影用；无锁遍历快照语义）。</summary>
    public IReadOnlyList<string> AvailableServices(string realm)
    {
        ValidateRealm(realm);
        var names = new List<string>();
        foreach (var kv in _services)
        {
            if (string.Equals(kv.Key.Realm, realm, StringComparison.Ordinal))
            {
                names.Add(kv.Key.Name);
            }
        }

        return names;
    }

    /// <summary>订阅变更（payload = 本批全部变更键；通知出锁触发）。退订后立即停止投递。</summary>
    public IDisposable Subscribe(Action<IReadOnlyList<ServiceKey>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_lock)
        {
            _handlers = [.. _handlers, handler];
        }

        return new Subscription(this, handler);
    }

    /// <summary>复合写（锁内）：属主校验 + 写 + 记变更；返回直发变更集（scope 激活时为 null——并入 scope）。</summary>
    private List<ServiceKey>? WriteWithOwnerCheck(ServiceKey key, object value, string ownerId)
    {
        lock (_lock)
        {
            if (_services.TryGetValue(key, out var existing)
                && !string.Equals(existing.OwnerId, ownerId, StringComparison.Ordinal))
            {
                throw new KeystoneException(
                    ErrorCode.ServiceAlreadyRegistered,
                    $"service '{key.Name}' (realm '{key.Realm}') has been registered by '{existing.OwnerId}'");
            }

            _services[key] = new Entry(value, ownerId);
            return RecordChange(key);
        }
    }

    /// <summary>复合删（锁内）：属主校验 + 删 + 记变更；键缺失幂等返回 null。</summary>
    private List<ServiceKey>? RemoveWithOwnerCheck(ServiceKey key, string ownerId)
    {
        lock (_lock)
        {
            if (!_services.TryGetValue(key, out var existing))
            {
                return null;
            }

            if (!string.Equals(existing.OwnerId, ownerId, StringComparison.Ordinal))
            {
                throw new KeystoneException(
                    ErrorCode.ServiceAlreadyRegistered,
                    $"service '{key.Name}' (realm '{key.Realm}') is owned by '{existing.OwnerId}'");
            }

            _services.TryRemove(key, out _);
            return RecordChange(key);
        }
    }

    /// <summary>记变更（须持锁）：scope 激活 → 并入 scope 返回 null；否则返回单键直发集。</summary>
    private List<ServiceKey>? RecordChange(ServiceKey key)
    {
        if (_activeScope is { } scope)
        {
            scope.Changes.Add(key);
            return null;
        }

        return [key];
    }

    /// <summary>scope dispose：弹出（若为栈顶），变更并入新栈顶或（栈空）出锁统一发。</summary>
    private void EndScope(NotifyScope scope)
    {
        List<ServiceKey>? toNotify = null;
        lock (_lock)
        {
            if (ReferenceEquals(_activeScope, scope))
            {
                _activeScope = scope.Parent;
            }

            if (_activeScope is { } top)
            {
                top.Changes.AddRange(scope.Changes);
            }
            else
            {
                // 集合语义（对齐 Cordis notify(names[])）：同键多次增删并入后只投递一次
                toNotify = scope.Changes.Distinct().ToList();
            }
        }

        if (toNotify is { Count: > 0 })
        {
            NotifyHandlers(toNotify);
        }
    }

    /// <summary>锁外直发（无 scope 时的单键变更）。</summary>
    private void NotifyIfPresent(List<ServiceKey>? changes)
    {
        if (changes is { Count: > 0 })
        {
            NotifyHandlers(changes);
        }
    }

    /// <summary>通知：锁内取订阅者快照，<b>出锁后</b>逐个投递（回调重入本 store 不死锁）。</summary>
    private void NotifyHandlers(List<ServiceKey> changes)
    {
        List<Action<IReadOnlyList<ServiceKey>>> handlers;
        lock (_lock)
        {
            handlers = [.. _handlers];
        }

        var payload = (IReadOnlyList<ServiceKey>)changes;
        foreach (var handler in handlers)
        {
            handler(payload);
        }
    }

    private static ServiceKey ValidateKey(string serviceName, string realm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ValidateRealm(realm);
        return new ServiceKey(serviceName, realm);
    }

    /// <summary>realm 校验："" = 默认共享域（合法）；仅拒 null 与非空纯空白。</summary>
    private static void ValidateRealm(string realm)
    {
        ArgumentNullException.ThrowIfNull(realm);
        if (realm.Length > 0 && string.IsNullOrWhiteSpace(realm))
        {
            throw new ArgumentException("The value cannot be composed entirely of whitespace.", nameof(realm));
        }
    }

    private sealed record Entry(object Value, string OwnerId);

    private sealed class NotifyScope(KeyedServiceStore store, NotifyScope? parent) : IDisposable
    {
        private int _ended;

        public NotifyScope? Parent { get; } = parent;

        public List<ServiceKey> Changes { get; } = [];

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _ended, 1) == 1)
            {
                return; // 双 dispose 幂等（累积集只被消费一次）
            }

            store.EndScope(this);
        }
    }

    private sealed class Disposer(KeyedServiceStore store, ServiceKey key, string ownerId) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return; // 双 dispose 幂等
            }

            store.Remove(key.Name, key.Realm, ownerId);
        }
    }

    private sealed class Subscription(KeyedServiceStore store, Action<IReadOnlyList<ServiceKey>> handler) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            lock (store._lock)
            {
                var updated = new List<Action<IReadOnlyList<ServiceKey>>>(store._handlers);
                updated.Remove(handler);
                store._handlers = updated;
            }
        }
    }
}
