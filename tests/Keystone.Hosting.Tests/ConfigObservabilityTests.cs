using System.Diagnostics;
using System.Diagnostics.Metrics;
using Keystone.Runtime.Trace;

namespace Keystone.Hosting.Tests;

/// <summary>
/// P70-T4（ADR-0018）：config/host 观测切片——apply/entry/hotupdate/group-tx span +
/// hotupdate.operations（hot|cold）+ writer.failures 计数接线。
/// ActivityListener/MeterListener 为进程级全局——本 collection 与 ObservabilityWiringTests
/// 共享 "Observability" 集合串行；断言用 Contains（其他测试的宿主也可能产生同类 span/计数）。
/// </summary>
[Collection("Observability")]
public class ConfigObservabilityTests
{
    private static KeystoneHostOptions Options(string? configFilePath = null) => new()
    {
        EnableCapabilityDomain = true,
        ConfigFilePath = configFilePath,
        ManifestProvider = _ => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
            "obs", "1.0.0", "Obs.cs", ["cordis-runtime"], [], []),
        SourceProvider = _ => new Keystone.Runtime.Plugins.Loading.PluginSource(
            "obs", ObservabilityWiringTests.MinimalPluginSource),
    };

    private static ActivityListener Spans(List<Activity> spans)
    {
        var gate = new object();
        return new ActivityListener
        {
            ShouldListenTo = s => s.Name == TraceContext.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = a =>
            {
                lock (gate)
                {
                    spans.Add(a);
                }
            },
        };
    }

    private static List<Activity> Snapshot(List<Activity> spans)
    {
        lock (spans)
        {
            return [.. spans];
        }
    }

    [Fact]
    public async Task ApplyConfigAsync_emits_apply_entry_and_group_transaction_spans()
    {
        var spans = new List<Activity>();
        using var listener = Spans(spans);
        ActivitySource.AddActivityListener(listener);

        await using var host = new KeystoneHost(Options());
        await host.StartAsync("- id: obs\n  name: ./obs\n");

        var newTree = Keystone.Config.Entries.EntryParser.Parse(
            "- id: obs\n  name: ./obs\n  config:\n    v: 2\n", null);
        await host.ApplyConfigAsync(newTree);

        var snapshot = Snapshot(spans);
        Assert.Contains(snapshot, a => a.OperationName == TraceContext.ConfigApplyActivityName);
        Assert.Contains(snapshot, a => a.OperationName == TraceContext.ConfigEntryActivityName
            && string.Equals(a.GetTagItem(TraceContext.EntryIdTag) as string, "obs", StringComparison.Ordinal));
        Assert.Contains(snapshot, a => a.OperationName == TraceContext.GroupTransactionActivityName
            && string.Equals(a.GetTagItem(TraceContext.OutcomeTag) as string, "applied", StringComparison.Ordinal));
        await host.ShutdownAsync();
    }

    [Fact]
    public async Task UpdatePlugin_emits_hotupdate_span_and_hot_channel_counter()
    {
        var spans = new List<Activity>();
        using var spanListener = Spans(spans);
        ActivitySource.AddActivityListener(spanListener);

        var channels = new List<string>();
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (i, l) =>
            {
                if (i.Meter.Name == KeystoneMeter.Name && i.Name == "keystone.hotupdate.operations")
                {
                    l.EnableMeasurementEvents(i);
                }
            },
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            channels.Add(tags.ToArray().FirstOrDefault(t => t.Key == TraceContext.ChannelTag).Value?.ToString() ?? "?"));
        meterListener.Start();

        await using var host = new KeystoneHost(Options());
        await host.StartAsync("- id: obs\n  name: ./obs\n");
        await host.UpdatePluginAsync("obs", new Dictionary<string, object?> { ["v"] = 2 });

        var snapshot = Snapshot(spans);
        Assert.Contains(snapshot, a => a.OperationName == TraceContext.HotUpdateActivityName
            && string.Equals(a.GetTagItem(TraceContext.EntryIdTag) as string, "obs", StringComparison.Ordinal));
        Assert.Contains(channels, c => c == "hot");
        await host.ShutdownAsync();
    }

    [Fact]
    public async Task ReloadPlugin_emits_cold_channel_counter()
    {
        var channels = new List<string>();
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (i, l) =>
            {
                if (i.Meter.Name == KeystoneMeter.Name && i.Name == "keystone.hotupdate.operations")
                {
                    l.EnableMeasurementEvents(i);
                }
            },
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            channels.Add(tags.ToArray().FirstOrDefault(t => t.Key == TraceContext.ChannelTag).Value?.ToString() ?? "?"));
        meterListener.Start();

        await using var host = new KeystoneHost(Options());
        await host.StartAsync("- id: obs\n  name: ./obs\n");
        await host.ReloadPluginAsync("obs");

        Assert.Contains(channels, c => c == "cold");
        await host.ShutdownAsync();
    }

    [Fact]
    public async Task Debounced_write_failure_increments_writer_failures_counter()
    {
        var failures = 0;
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (i, l) =>
            {
                if (i.Meter.Name == KeystoneMeter.Name && i.Name == "keystone.writer.failures")
                {
                    l.EnableMeasurementEvents(i);
                }
            },
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, _, _) => failures++);
        meterListener.Start();

        var missingDir = Path.Combine(Path.GetTempPath(), "keystone-obs-" + Guid.NewGuid().ToString("N"));
        var configPath = Path.Combine(missingDir, "config.yaml");
        await using var host = new KeystoneHost(Options(configPath));
        await host.StartAsync("- id: obs\n  name: ./obs\n");

        await host.MoveEntryAsync("obs", null); // 触发防抖写回（Timer 丢弃路径 → OnWriteFailed → writer.failures）

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (failures == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50, CancellationToken.None);
        }

        Assert.True(failures >= 1);
        await host.ShutdownAsync();
    }
}
