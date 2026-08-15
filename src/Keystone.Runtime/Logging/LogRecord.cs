using Microsoft.Extensions.Logging;

namespace Keystone.Runtime.Logging;

/// <summary>结构化日志记录（05 §5 模型：Timestamp/TaskId/Category/Level/Message）。</summary>
public sealed record LogRecord(
    DateTimeOffset Timestamp,
    Guid? TaskId,
    string Category,
    LogLevel Level,
    string Message,
    Exception? Exception = null);

