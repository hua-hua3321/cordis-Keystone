using Keystone.Runtime.Logging;
using Microsoft.Extensions.Logging;

namespace Keystone.Runtime.Tests;

/// <summary>
/// G-C7 日志导出器测试（16-cordis-gap-review）：sink 抽象（对齐 Cordis Exporter）——
/// 可注入输出目标，每条记录分发到全部 sink；Console sink 输出结构化行。
/// </summary>
public class LogSinkTests
{
    private sealed class CollectingSink(List<LogRecord> records) : ILogSink
    {
        public void Write(LogRecord record) => records.Add(record);
    }

    [Fact]
    public void Log_records_are_dispatched_to_injected_sinks()
    {
        var received = new List<LogRecord>();
        var provider = new RingBufferLoggerProvider(sinks: [new CollectingSink(received)]);
        var logger = provider.CreateLogger("fs/reader");

        logger.LogInformation("read {Bytes} bytes", 42);

        Assert.Single(received);
        Assert.Equal("fs/reader", received[0].Category);
        Assert.Equal(LogLevel.Information, received[0].Level);
        Assert.Equal("read 42 bytes", received[0].Message);
    }

    [Fact]
    public void Console_sink_writes_structured_line()
    {
        var writer = new StringWriter();
        var sink = new ConsoleLogSink(writer: writer);
        var provider = new RingBufferLoggerProvider(sinks: [sink]);
        var logger = provider.CreateLogger("audit");

        logger.LogWarning("slow operation");

        var output = writer.ToString();
        Assert.Contains("audit", output);
        Assert.Contains("slow operation", output);
        Assert.Contains("Warning", output);
    }

    [Fact]
    public void Ansi_console_sink_colorizes_level()
    {
        var writer = new StringWriter();
        var sink = new ConsoleLogSink(ansi: true, writer: writer);
        var provider = new RingBufferLoggerProvider(sinks: [sink]);
        var logger = provider.CreateLogger("fs");

        logger.LogError("boom");

        Assert.Contains("\u001b[31m", writer.ToString()); // 红 = Error
    }

    [Fact]
    public void Buffer_snapshot_still_works_with_sinks()
    {
        var provider = new RingBufferLoggerProvider(sinks: [new ConsoleLogSink(writer: new StringWriter())]);
        provider.CreateLogger("x").LogInformation("hello");

        Assert.Single(provider.GetSnapshot());
        Assert.Equal("hello", provider.GetSnapshot()[0].Message);
    }
}
