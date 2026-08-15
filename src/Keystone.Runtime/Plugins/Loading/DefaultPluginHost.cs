namespace Keystone.Runtime.Plugins.Loading;

/// <summary>同进程 ALC 宿主（DC-19，ADR-0001 决策 1 方案 A；本期默认且唯一形态）。</summary>
public sealed class DefaultPluginHost : IPluginHost
{
    public static DefaultPluginHost Instance { get; } = new();

    public string IsolationModel => "same-process-alc";
}
