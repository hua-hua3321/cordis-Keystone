using Keystone.Runtime.Logging;
using Microsoft.Extensions.Logging;

namespace Keystone.Runtime.Tests;

/// <summary>
/// G-C11 日志级别默认阈值测试（16-cordis-gap-review）：三级过滤——
/// 按 category 覆盖 → defaultLevel → 全局默认 Information（对齐 Cordis levels[name] ?? levels.default ?? INFO）。
/// </summary>
public class LogLevelThresholdTests
{
    [Fact]
    public void Default_threshold_is_information()
    {
        var provider = new RingBufferLoggerProvider();
        var logger = provider.CreateLogger("fs");

        Assert.True(logger.IsEnabled(LogLevel.Information));
        Assert.False(logger.IsEnabled(LogLevel.Debug)); // 无 override：默认 INFO，Debug 被过滤
        Assert.False(logger.IsEnabled(LogLevel.Trace));
    }

    [Fact]
    public void Category_override_beats_default()
    {
        var provider = new RingBufferLoggerProvider(
            overrides: new Dictionary<string, LogLevel> { ["verbose"] = LogLevel.Debug });
        var debugLogger = provider.CreateLogger("verbose");
        var normalLogger = provider.CreateLogger("other");

        Assert.True(debugLogger.IsEnabled(LogLevel.Debug));   // 按名覆盖 → Debug 放行
        Assert.False(normalLogger.IsEnabled(LogLevel.Debug)); // 无覆盖 → 默认 INFO 过滤
    }

    [Fact]
    public void Default_level_parameter_applies_to_uncovered_categories()
    {
        var provider = new RingBufferLoggerProvider(defaultLevel: LogLevel.Warning);
        var logger = provider.CreateLogger("audit");

        Assert.True(logger.IsEnabled(LogLevel.Warning));
        Assert.False(logger.IsEnabled(LogLevel.Information)); // default=Warning → Info 过滤
    }

    [Fact]
    public void Category_override_still_wins_over_default_level()
    {
        var provider = new RingBufferLoggerProvider(
            overrides: new Dictionary<string, LogLevel> { ["noisy"] = LogLevel.Trace },
            defaultLevel: LogLevel.Warning);
        var noisy = provider.CreateLogger("noisy");
        var quiet = provider.CreateLogger("quiet");

        Assert.True(noisy.IsEnabled(LogLevel.Trace)); // 按名覆盖优先
        Assert.False(quiet.IsEnabled(LogLevel.Trace));
    }

    [Fact]
    public void Filtered_records_are_not_written()
    {
        var provider = new RingBufferLoggerProvider();
        var logger = provider.CreateLogger("fs");

        logger.LogDebug("hidden");
        logger.LogInformation("visible");

        Assert.Single(provider.GetSnapshot()); // Debug 被过滤
        Assert.Equal("visible", provider.GetSnapshot()[0].Message);
    }
}
