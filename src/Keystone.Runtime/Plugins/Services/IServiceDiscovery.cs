using Keystone.Runtime.Context;

namespace Keystone.Runtime.Plugins.Services;

/// <summary>
/// 服务发现层（18 §2 CA-1 第 2 步，P57-T4）：值层的只读+通知投影——"谁可用"与"变更唤醒"。
/// 契约（ID-52）：仅元数据（值不经此层传递，值解析走 <see cref="KeyedServiceStore"/>/context 链）；
/// <see cref="IsAvailable"/> 永远同步本地读（未来分布式 adapter = 本地缓存 + 后台同步，网络不上门控热路径）；
/// 通知为批量变更键（对齐 Cordis notify(names[])）。
/// </summary>
public interface IServiceDiscovery
{
    /// <summary>可用 = 值存在（单一事实源投影）。</summary>
    bool IsAvailable(string serviceName, string realm);

    /// <summary>指定域内可用服务名（诊断）。</summary>
    IReadOnlyList<string> AvailableServices(string realm);

    /// <summary>订阅变更（payload = 本批全部变更键，含增与删）。</summary>
    IDisposable Subscribe(Action<IReadOnlyList<ServiceKey>> handler);
}
