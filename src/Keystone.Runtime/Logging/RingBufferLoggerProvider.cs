using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Keystone.Runtime.Logging;

/// <summary>
/// 环形缓冲日志 provider（L1）：内存环形 1000 条 + 诊断快照可读。
/// 级别过滤（G12 + G-C11）：三级阈值——按 category 覆盖 → defaultLevel → 全局默认 Information
/// （对齐 Cordis levels[name] ?? levels.default ?? INFO，logger.ts:155）。
/// </summary>
public sealed class RingBufferLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogRecord> _buffer;
    private readonly int _capacity;
    private readonly IReadOnlyDictionary<string, LogLevel> _overrides;
    private readonly IReadOnlyList<ILogSink> _sinks;
    private readonly LogLevel? _defaultLevel;

    public RingBufferLoggerProvider(
        int capacity = 1000,
        IReadOnlyDictionary<string, LogLevel>? overrides = null,
        IReadOnlyList<ILogSink>? sinks = null,
        LogLevel? defaultLevel = null)
    {
        _capacity = capacity;
        _buffer = new ConcurrentQueue<LogRecord>();
        _overrides = overrides ?? new Dictionary<string, LogLevel>(StringComparer.Ordinal);
        _sinks = sinks ?? []; // G-C7：sink 可注入（Console 默认由宿主接线，05 §5）
        _defaultLevel = defaultLevel; // G-C11：default 兜底（null = 全局默认 Information）
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

    /// <summary>
    /// G-C11 三级级别过滤（对齐 Cordis levels[name] ?? levels.default ?? INFO）：
    /// 按 category 覆盖 → defaultLevel → 全局默认 Information。
    /// </summary>
    internal bool IsEnabled(string categoryName, LogLevel level)
    {
        var min = _overrides.TryGetValue(categoryName, out var overrideLevel)
            ? overrideLevel
            : _defaultLevel ?? LogLevel.Information;
        return level >= min;
    }

    /// <summary>P2-21（19 号审计 LG-21）：Dispose 断言面——宿主 ShutdownAsync 经自建 factory 传导。</summary>
    public bool IsDisposed { get; private set; }

    public void Dispose() => IsDisposed = true;

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
