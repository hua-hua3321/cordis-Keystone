namespace Keystone.Runtime.Plugins.Lifecycle;

/// <summary>状态迁移通知（internal/status 对应物，ADR-0005）。</summary>
public sealed class LifecycleStateChangedEventArgs : EventArgs
{
    public LifecycleStateChangedEventArgs(PluginLifecycleState state)
    {
        State = state;
    }

    public PluginLifecycleState State { get; }
}
