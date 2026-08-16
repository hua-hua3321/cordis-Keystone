using Microsoft.Extensions.Logging;

namespace Keystone.Hosting.Tests;

/// <summary>
/// P70-T5（ADR-0018 L1）：插件生命周期结构化日志——start 成功 / 失败归因 / stop
/// 经 PluginRuntime LoggerMessage 源生成（6101-6103），category = {能力域}/{插件 ID}。
/// 修复前：插件生命周期只有事实事件（PluginStartedFact/PluginFailedFact），无结构化日志。
/// </summary>
public class PluginLifecycleLoggingTests
{
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<(string Category, LogLevel Level, string Message)> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(CapturingLoggerProvider owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => owner.Entries.Add((category, logLevel, formatter(state, exception)));
        }
    }

    private const string OkSource = """
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        public sealed class OkPlugin : IPlugin
        {
            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
                => Task.CompletedTask;

            public Task DisposeAsync() => Task.CompletedTask;
        }
        """;

    private const string FailSource = """
        using System;
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        public sealed class FailPlugin : IPlugin
        {
            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
                => throw new InvalidOperationException("init boom");

            public Task DisposeAsync() => Task.CompletedTask;
        }
        """;

    private static KeystoneHostOptions Options(ILoggerFactory factory) => new()
    {
        CapabilityDomainName = "fs",
        LoggerFactory = factory,
        ManifestProvider = e => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
            e.Id!, "1.0.0", "X.cs", ["cordis-runtime"], [], []),
        SourceProvider = e => new Keystone.Runtime.Plugins.Loading.PluginSource(
            e.Id!, string.Equals(e.Id, "boom", StringComparison.Ordinal) ? FailSource : OkSource),
    };

    [Fact]
    public async Task Plugin_start_and_stop_emit_structured_logs()
    {
        var provider = new CapturingLoggerProvider();
        await using var host = new KeystoneHost(Options(LoggerFactory.Create(b => b.AddProvider(provider))));
        await host.StartAsync("- id: ok\n  name: ./ok\n");
        await host.ShutdownAsync();

        Assert.Contains(provider.Entries, e =>
            e.Level == LogLevel.Information && e.Message.Contains("started") && e.Category == "fs/ok");
        Assert.Contains(provider.Entries, e =>
            e.Level == LogLevel.Information && e.Message.Contains("stopped") && e.Category == "fs/ok");
    }

    [Fact]
    public async Task Plugin_init_failure_emits_failed_log()
    {
        var provider = new CapturingLoggerProvider();
        await using var host = new KeystoneHost(Options(LoggerFactory.Create(b => b.AddProvider(provider))));
        await host.StartAsync("- id: boom\n  name: ./boom\n");
        await host.ShutdownAsync();

        Assert.Contains(provider.Entries, e =>
            e.Level == LogLevel.Error && e.Message.Contains("failed") && e.Message.Contains("boom"));
    }
}
