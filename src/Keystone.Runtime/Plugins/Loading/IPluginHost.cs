namespace Keystone.Runtime.Plugins.Loading;

/// <summary>
/// 插件运行形态扩展点（DC-19，ADR-0001 决策 1）：**预留**——本期唯一实现为同进程 ALC
/// （<see cref="DefaultPluginHost"/>）；独立进程隔离（方案 B）作为未来可选形态经此接口引入，
/// 不进入本期默认配置。
/// </summary>
public interface IPluginHost
{
    /// <summary>隔离模型描述符（观测/装配分流依据）。</summary>
    string IsolationModel { get; }
}
