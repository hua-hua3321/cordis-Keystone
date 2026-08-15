using Microsoft.Extensions.Logging;

namespace Keystone.Hosting.Tests;

/// <summary>
/// DC-20（17-doc-compliance-audit / 05 §5）：日志 category 前缀 + 宿主 LoggerFactory 接线。
/// 修复前：category 无域前缀；宿主未接 loggerFactory（插件 logger = NullLogger）。
/// 兑现：category = {能力域}/{插件 ID}；KeystoneHostOptions.LoggerFactory 注入根 context
/// → 插件 context 复用（日志经 RingBufferLoggerProvider/自定义 provider 可见）。
/// </summary>
public class LoggingCategoryTests
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

    private const string LoggingSource = """
        using System;
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Microsoft.Extensions.Logging;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        public sealed class LoggingPlugin : IPlugin
        {
            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
            {
                context.Logger.LogInformation("plugin initialized");
                return Task.CompletedTask;
            }

            public Task DisposeAsync() => Task.CompletedTask;
        }
        """;

    [Fact]
    public async Task Plugin_logs_carry_domain_category_prefix()
    {
        // category = {能力域}/{插件 ID}（05 §5 命名规则）——插件 logger 経宿主 LoggerFactory 可见
        var provider = new CapturingLoggerProvider();
        var host = new KeystoneHost(new KeystoneHostOptions
        {
            ManifestProvider = e => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
                e.Id!, "1.0.0", "X.cs", ["cordis-runtime"], [], []),
            SourceProvider = e => new Keystone.Runtime.Plugins.Loading.PluginSource(e.Id!, LoggingSource),
            CapabilityDomainName = "fs",
            LoggerFactory = LoggerFactory.Create(b => b.AddProvider(provider)),
        });

        await host.StartAsync("- id: storage\n  name: ./plugins/storage\n");

        var entry = Assert.Single(provider.Entries, e => e.Message == "plugin initialized");
        Assert.Equal("fs/storage", entry.Category); // {能力域}/{插件 ID}
    }

    [Fact]
    public async Task Without_factory_plugins_fall_back_to_null_logger()
    {
        // 未配置 LoggerFactory：原行为（NullLogger）保持——初始化不抛
        var host = new KeystoneHost(new KeystoneHostOptions
        {
            ManifestProvider = e => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
                e.Id!, "1.0.0", "X.cs", ["cordis-runtime"], [], []),
            SourceProvider = e => new Keystone.Runtime.Plugins.Loading.PluginSource(e.Id!, LoggingSource),
        });

        await host.StartAsync("- id: storage\n  name: ./plugins/storage\n");

        Assert.Equal(
            Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active,
            host.GetPluginState("storage"));
    }
}
