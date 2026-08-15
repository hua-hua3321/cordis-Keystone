namespace Keystone.Runtime.Logging;

/// <summary>
/// 日志输出目标（sink）抽象（G-C7，对齐 Cordis Exporter，logger.ts:41-47）：
/// 可插拔输出——Console/File/远端等，随 LoggerProvider 注册，每条记录分发到全部 sink。
/// </summary>
public interface ILogSink
{
    /// <summary>输出一条结构化日志记录。</summary>
    void Write(LogRecord record);
}
