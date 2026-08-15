namespace Keystone.Runtime.Context;

/// <summary>
/// 上下文门面拦截器（H3 定稿，doc 12 §7.3）：服务访问拦截（internal/get|set 对应物）。
/// AOT 安全路径 = 显式接口 + 门面组合（无 Castle/DispatchProxy 运行时代理，规则 0）。
/// </summary>
public interface IContextInterceptor
{
    /// <summary>服务读取前通知（Get/TryGet）。</summary>
    ValueTask OnServiceReadAsync(string serviceName, CancellationToken cancellationToken);

    /// <summary>服务写入前通知（Provide/Set）。</summary>
    ValueTask OnServiceWriteAsync(string serviceName, object? value, CancellationToken cancellationToken);
}
