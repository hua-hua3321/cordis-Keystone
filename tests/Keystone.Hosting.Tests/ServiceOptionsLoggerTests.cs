using Keystone.Runtime.Plugins.Manifest;

namespace Keystone.Hosting.Tests;

/// <summary>
/// CA-12 服务级配置合并链 intercept 对应物（18 §2 P1，P60）——日志首例：
/// 修复前 RingBufferLoggerProvider 构造已支持 overrides/defaultLevel/capacity 但无人接线——
/// 宿主未配 LoggerFactory 时 root 走 NullLoggerFactory（RingBuffer 根本不在链上）。
/// 兑现：ServiceOptions["logger"] = { defaultLevel, capacity, levels: {category→level} }；
/// 显式 LoggerFactory 优先（ServiceOptions 被忽略）。
/// </summary>
public class ServiceOptionsLoggerTests
{
    private const string LoggingPluginSource = """
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Microsoft.Extensions.Logging;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        public sealed class LoggingPlugin : IPlugin
        {
            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
            {
                context.Logger.LogDebug("dbg-message");
                context.Logger.LogError("err-message");
                return Task.CompletedTask;
            }

            public Task DisposeAsync() => Task.CompletedTask;
        }
        """;

    private static KeystoneHostOptions Options(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? serviceOptions = null,
        Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory = null)
    {
        var options = new KeystoneHostOptions
        {
            ManifestProvider = _ => new PluginManifest("logp", "1.0.0", "P.cs", ["cordis-runtime"], [], []),
            SourceProvider = _ => new Keystone.Runtime.Plugins.Loading.PluginSource("logp", LoggingPluginSource),
            ServiceOptions = serviceOptions,
            LoggerFactory = loggerFactory,
        };
        return options;
    }

    [Fact]
    public async Task ServiceOptions_logger_filters_plugin_logs()
    {
        // levels.{category}=Error → 该 category 的 Debug 不进环形缓冲（三级阈值：覆盖 → default → Information）
        var serviceOptions = new Dictionary<string, IReadOnlyDictionary<string, object?>>
        {
            ["logger"] = new Dictionary<string, object?>
            {
                ["levels"] = new Dictionary<string, object?> { ["keystone/logp"] = "Error" },
            },
        };
        await using var host = new KeystoneHost(Options(serviceOptions));
        await host.StartAsync("- id: logp\n  name: ./plugins/logp\n");

        Assert.NotNull(host.RingBufferLogs);
        var snapshot = host.RingBufferLogs!.GetSnapshot();
        Assert.DoesNotContain(snapshot, r => r.Message == "dbg-message"); // 被级别过滤
        Assert.Contains(snapshot, r => r.Message == "err-message"); // Error 放行

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task ServiceOptions_logger_default_level_applies()
    {
        // defaultLevel=Warning → 未覆盖 category 的 Information 日志不进缓冲
        var serviceOptions = new Dictionary<string, IReadOnlyDictionary<string, object?>>
        {
            ["logger"] = new Dictionary<string, object?> { ["defaultLevel"] = "Warning" },
        };
        await using var host = new KeystoneHost(Options(serviceOptions));
        await host.StartAsync("- id: logp\n  name: ./plugins/logp\n");

        Assert.NotNull(host.RingBufferLogs);
        var snapshot = host.RingBufferLogs!.GetSnapshot();
        Assert.DoesNotContain(snapshot, r => r.Message == "dbg-message"); // Debug < Warning
        Assert.Contains(snapshot, r => r.Message == "err-message"); // Error ≥ Warning

        await host.ShutdownAsync();
    }

    [Fact]
    public async Task Explicit_LoggerFactory_wins_over_service_options()
    {
        // 显式 LoggerFactory → ServiceOptions["logger"] 被忽略（嵌入方优先，不覆盖）
        var serviceOptions = new Dictionary<string, IReadOnlyDictionary<string, object?>>
        {
            ["logger"] = new Dictionary<string, object?> { ["defaultLevel"] = "None" },
        };
        var factory = Microsoft.Extensions.Logging.LoggerFactory.Create(_ => { });
        await using var host = new KeystoneHost(Options(serviceOptions, factory));
        await host.StartAsync("- id: logp\n  name: ./plugins/logp\n");

        Assert.Null(host.RingBufferLogs); // 未走 RingBuffer 接线（嵌入方工厂在用）

        await host.ShutdownAsync();
        factory.Dispose();
    }

    [Fact]
    public async Task Without_logger_options_ring_buffer_absent()
    {
        // 无 ServiceOptions 也无 LoggerFactory → 现状保持（NullLoggerFactory 兜底，RingBuffer 不在链上）
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("- id: logp\n  name: ./plugins/logp\n");

        Assert.Null(host.RingBufferLogs);

        await host.ShutdownAsync();
    }
}
