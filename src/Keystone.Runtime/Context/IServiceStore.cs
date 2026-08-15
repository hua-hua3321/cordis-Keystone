namespace Keystone.Runtime.Context;

/// <summary>
/// 服务存储（P2 最小面，03 §2.1/§2.3）：按服务名注册/解析，同 scope 重复注册报错（rebind，G14），
/// 属主校验（G8，非属主覆盖报错）。完整依赖门控（PENDING→ACTIVE）在 P3（ADR-0007）。
/// </summary>
public interface IServiceStore
{
    /// <summary>
    /// 注册/更新服务。已注册且属主不同 → KeystoneException（ServiceAlreadyRegistered，rebind/属主冲突）。
    /// </summary>
    void Set<T>(string serviceName, T value, string ownerId);

    /// <summary>读取服务；缺失返回默认值。</summary>
    T? TryGet<T>(string serviceName);

    /// <summary>读取服务；缺失抛 KeystoneException（GatingServiceNotFound）。</summary>
    T Get<T>(string serviceName);

    /// <summary>
    /// 注销服务（G-C3 值卸载，16-cordis-gap-review）：属主校验后移除。
    /// 属主不匹配 → KeystoneException（ServiceAlreadyRegistered 语义：非属主不可移除）。
    /// </summary>
    void Remove(string serviceName, string ownerId);
}
