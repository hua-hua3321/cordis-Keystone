using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Keystone.Runtime.Logging;

/// <summary>
/// 环形缓冲日志 provider（L1）：内存环形 1000 条 + 诊断快照可读。
/// 类别级别覆盖（G12）：overrides 字典（category → 最低级别）。
/// </summary>
public sealed class RingBufferLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogRecord> _buffer;
    private readonly int _capacity;
    private readonly IReadOnlyDictionary<string, LogLevel> _overrides;
    private readonly IReadOnlyList<ILogSink> _sinks;

    public RingBufferLoggerProvider(
        int capacity = 1000,
        IReadOnlyDictionary<string, LogLevel>? overrides = null,
        IReadOnlyList<ILogSink>? sinks = null)
    {
        _capacity = capacity;
        _buffer = new ConcurrentQueue<LogRecord>();
        _overrides = overrides ?? new Dictionary<string, LogLevel>(StringComparer.Ordinal);
        _sinks = sinks ?? []; // G-C7：sink 可注入（Console 默认由宿主接线，05 §5）
    }

    public ILogger CreateLogger(string categoryName) => new RingLogger(this, categoryName);

    /// <summary>诊断快照（环形缓冲当前内容，最新在尾部）。</summary>
    public IReadOnlyList<LogRecord> GetSnapshot() => [.. _buffer];

    internal void Write(string categoryName, LogLevel level, Guid? taskId, string message, Exception? exception = null)
    {
        var record = new LogRecord(DateTimeOffset.UtcNow, taskId, categoryName, level, message, exception);
        _buffer.Enqueue(record);
        while (_buffer.Count > _capacity)
        {
            _buffer.TryDequeue(out _);
        }

        // G-C7：分发到全部输出目标（Console/File/远端）
        foreach (var sink in _sinks)
        {
            sink.Write(record);
        }
    }

    internal bool IsEnabled(string categoryName, LogLevel level)
        => _overrides.TryGetValue(categoryName, out var min) ? level >= min : true;

    public void Dispose()
    {
    }

    private sealed class RingLogger : ILogger
    {
        private readonly RingBufferLoggerProvider _provider;
        private readonly string _category;

        public RingLogger(RingBufferLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => _provider.IsEnabled(_category, logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            var taskId = Trace.TraceContext.GetCurrentTaskId();
            _provider.Write(_category, logLevel, taskId == default ? null : taskId.Value, message, exception); // L4：异常结构化字段
        }
    }
}
