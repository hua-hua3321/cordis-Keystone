namespace Keystone.Runtime.Actors;

/// <summary>
/// 能力域监督策略选项（05 §2/09 §3，DC-4）：
/// OneForOne（默认 Restart decider）——崩溃重启，连续失败超阈值 → 停止不再重启（域不可用）。
/// 指数退避由 Proto.Actor 监督窗口语义承担（withinTimeSpan 窗口内计数）。
/// </summary>
public sealed record CapabilitySupervisionOptions
{
    /// <summary>窗口内最大重启次数（超阈值停止 = 域不可用，05 §2 升级语义）。</summary>
    public int MaxRestarts { get; init; } = 3;

    /// <summary>重启计数窗口（该时间窗内超过 MaxRestarts 次失败 → 停止）。</summary>
    public TimeSpan RestartWindow { get; init; } = TimeSpan.FromSeconds(5);
}
