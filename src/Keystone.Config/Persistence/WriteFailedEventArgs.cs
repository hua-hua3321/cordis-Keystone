namespace Keystone.Config.Persistence;

/// <summary>
/// P2-24（19 号审计 IN-4）：防抖冲刷失败事件参数——Timer 丢弃路径的异常载体
///（重试耗尽等；对齐 Cordis logger.warn 的可观测面）。
/// </summary>
public sealed class WriteFailedEventArgs(Exception exception) : EventArgs
{
    /// <summary>写失败异常（已包 KeystoneException 语义，P2-26）。</summary>
    public Exception Exception { get; } = exception;
}
