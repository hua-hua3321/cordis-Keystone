using Keystone.Runtime.Context;

namespace Keystone.Runtime.Plugins.Lifecycle;

/// <summary>
/// 插件契约（02-plugin-model §6 disposer 原语；10-plugin-sdk §2 接口面）。
/// 状态机叠加在其上（ADR-0005），不改变本接口形状。
/// </summary>
public interface IPlugin
{
    /// <summary>插件初始化（配置校验后调用；抛错 → 进入 FAILED 态）。</summary>
    Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config);

    /// <summary>释放资源（quiesce 逆序收敛的一部分）。</summary>
    Task DisposeAsync();
}
