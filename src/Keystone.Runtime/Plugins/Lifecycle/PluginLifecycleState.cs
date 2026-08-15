namespace Keystone.Runtime.Plugins.Lifecycle;

/// <summary>
/// 插件生命周期状态机（ADR-0005 决策 1）：
/// PENDING → LOADING → ACTIVE → FAILED → UNLOADING → DISPOSED（FAILED 可经 restart 回 LOADING）。
/// </summary>
public enum PluginLifecycleState
{
    /// <summary>依赖未就绪，等待（ADR-0007 门控）。</summary>
    Pending,

    /// <summary>加载中（InitializeAsync）。</summary>
    Loading,

    /// <summary>运行中（服务已注册）。</summary>
    Active,

    /// <summary>启动失败（持有错误，可 restart）。</summary>
    Failed,

    /// <summary>卸载收敛中（quiesce 五步闸门）。</summary>
    Unloading,

    /// <summary>终态（已卸载）。</summary>
    Disposed,
}
