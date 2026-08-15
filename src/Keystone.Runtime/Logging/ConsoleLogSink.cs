using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Keystone.Runtime.Logging;

/// <summary>
/// 控制台日志输出（G-C7，05-reliability §5 "Console（默认）"）：
/// 结构化行输出，可选 ANSI 级别配色（对齐 Cordis 彩色输出，logger.ts:84-97）。
/// </summary>
public sealed class ConsoleLogSink : ILogSink
{
    private readonly bool _ansi;
    private readonly TextWriter _writer;

    public ConsoleLogSink(bool ansi = false, TextWriter? writer = null)
    {
        _ansi = ansi;
        _writer = writer ?? Console.Out;
    }

    /// <inheritdoc />
    public void Write(LogRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var level = _ansi ? Colorize(record.Level) : record.Level.ToString();
        var task = record.TaskId is { } id ? $" [{id:N}]" : string.Empty;
        var exception = record.Exception is null ? string.Empty : $" {record.Exception}";
        _writer.WriteLine(
            string.Format(
                CultureInfo.InvariantCulture,
                "{0:O} {1,-11} {2}{3} {4}{5}",
                record.Timestamp,
                level,
                record.Category,
                task,
                record.Message,
                exception));
    }

    private static string Colorize(LogLevel level)
    {
        var code = level switch
        {
            LogLevel.Critical => "41",  // 红底
            LogLevel.Error => "31",     // 红
            LogLevel.Warning => "33",   // 黄
            LogLevel.Information => "36", // 青
            LogLevel.Debug => "90",     // 亮灰
            _ => "0",
        };
        return $"\u001b[{code}m{level}\u001b[0m";
    }
}
