using System.Diagnostics;
using Keystone.Runtime.Context;
using Keystone.Runtime.Events;
using Keystone.Runtime.Plugins.Lifecycle;
using Keystone.Runtime.Plugins.Loading;
using Keystone.Runtime.Plugins.Manifest;
using Keystone.Runtime.Plugins.Services;

namespace Keystone.Runtime.Tests;

/// <summary>
/// 性能冒烟（13 P13 验收 3）：加载/卸载/事件吞吐基线——宽松断言防 flaky，耗时输出作基线记录。
/// </summary>
public class PerformanceSmokeTests
{
    private static readonly PluginManifest Manifest =
        new("perf", "1.0.0", "Perf.cs", ["cordis-runtime"], [], []);

    [Fact]
    public async Task Plugin_load_unload_cycle_baseline()
    {
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 10; i++)
        {
            await using var loader = await PluginLoader.CreateAsync(
                new PluginSource($"perf{i}", SampleSources.V1),
                Manifest,
                new InMemoryServiceDiscovery(new KeyedServiceStore()),
                id => new Context.ContextFacade(id));
            Assert.Equal(PluginLifecycleState.Active, loader.Runtime.State);
        }

        sw.Stop();
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(30), $"10 次加载/卸载循环应 < 30s，实际 {sw.Elapsed}");
        OutputBaseline($"插件加载/卸载循环 10 次", sw.Elapsed);
    }

    [Fact]
    public async Task Event_publish_throughput_baseline()
    {
        var bus = new EventBus();
        var received = 0;
        bus.Subscribe<Ping>(_ => Interlocked.Increment(ref received));

        const int N = 10_000;
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < N; i++)
        {
            await bus.EmitAsync(new Ping("x"));
        }

        sw.Stop();
        Assert.Equal(N, received);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"10000 条 emit 应 < 10s，实际 {sw.Elapsed}");
        OutputBaseline($"事件 emit 吞吐 {N} 条", sw.Elapsed);
    }

    [Fact]
    public async Task Pipeline_invoke_throughput_baseline()
    {
        var builder = new Pipeline.PipelineBuilder();
        builder.AddMiddleware(new NoopMiddleware());
        var pipeline = builder.Build();
        var ctx = new Context.ContextFacade("perf");

        const int N = 10_000;
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < N; i++)
        {
            await pipeline.InvokeAsync(ctx);
        }

        sw.Stop();
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"10000 次管道调用应 < 10s，实际 {sw.Elapsed}");
        OutputBaseline($"管道调用吞吐 {N} 次", sw.Elapsed);
    }

    private static void OutputBaseline(string label, TimeSpan elapsed)
    {
        // 基线记录（14 日志 P13 汇总）；测试输出可见
        Console.WriteLine($"[perf-baseline] {label}: {elapsed.TotalMilliseconds:F1} ms");
    }

    private sealed class NoopMiddleware : Pipeline.IMiddleware
    {
        public string Id => "noop";

        public int Order => 0;

        public Task InvokeAsync(IPluginContext ctx, Pipeline.RequestDelegate next) => next(ctx);
    }
}
