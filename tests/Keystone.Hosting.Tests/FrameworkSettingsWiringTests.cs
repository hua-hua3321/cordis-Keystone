using System.Diagnostics;
using Keystone.Core;
using Keystone.Runtime.Plugins.Lifecycle;

namespace Keystone.Hosting.Tests;

/// <summary>
/// P71-T1（硬编码审计批）：KeystoneSettings 配置面接线验收。
/// 修复前：Core 的 KeystoneSettings.Bind 存在但宿主零消费，PluginLoader 两处 new PluginRuntime
/// 均不传超时——keystone 节里写 DependencyWaitTimeout/QuiesceTimeout 完全无效（配置基础设施断线）。
/// 修复后：KeystoneHostOptions.FrameworkSettings → PluginLoader.CreateAsync → PluginRuntime 全程贯穿。
/// </summary>
public class FrameworkSettingsWiringTests
{
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

    private const string SlowDisposeSource = """
        using System;
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        public sealed class SlowDisposePlugin : IPlugin
        {
            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
                => Task.CompletedTask;

            public Task DisposeAsync() => Task.Delay(TimeSpan.FromSeconds(6));
        }
        """;

    private const string BoomSource = """
        using System;
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        public sealed class BoomPlugin : IPlugin
        {
            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
                => throw new InvalidOperationException("provider boom");

            public Task DisposeAsync() => Task.CompletedTask;
        }
        """;

    private static KeystoneHostOptions Options(KeystoneSettings settings) => new()
    {
        FrameworkSettings = settings,
        // provider 声明 provides ["svc"]（过静态可达性校验）但 init 抛异常 → svc 运行期永不出现
        // → waiter PENDING → 依赖等待超时（静态校验拒绝的是"无人声明提供"，走不到运行期门控）
        ManifestProvider = e => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
            e.Id!, "1.0.0", "X.cs", ["cordis-runtime"],
            string.Equals(e.Id, "provider", StringComparison.Ordinal) ? ["svc"] : [],
            string.Equals(e.Id, "waiter", StringComparison.Ordinal) ? ["svc"] : []),
        SourceProvider = e => new Keystone.Runtime.Plugins.Loading.PluginSource(
            e.Id!,
            string.Equals(e.Id, "provider", StringComparison.Ordinal) ? BoomSource
                : string.Equals(e.Id, "slowdispose", StringComparison.Ordinal) ? SlowDisposeSource
                : OkSource),
    };

    [Fact]
    public async Task Dependency_wait_timeout_flows_from_framework_settings()
    {
        // 300ms 生效 → FAILED 秒级出现；断线时走 30s 默认，12s 内不会 Failed
        await using var host = new KeystoneHost(Options(
            new KeystoneSettings { DependencyWaitTimeout = TimeSpan.FromMilliseconds(300) }));
        var stopwatch = Stopwatch.StartNew();
        await host.StartAsync("- id: provider\n  name: ./provider\n- id: waiter\n  name: ./waiter\n");

        var failed = false;
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(12))
        {
            if (host.GetPluginState("waiter") == PluginLifecycleState.Failed)
            {
                failed = true;
                break;
            }

            await Task.Delay(50);
        }

        stopwatch.Stop();
        Assert.True(failed, "依赖缺失应在框架超时内转 FAILED");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"超时应来自 FrameworkSettings 而非 30s 默认（实际耗时 {stopwatch.Elapsed}）");
    }

    [Fact]
    public async Task Quiesce_timeout_flows_from_framework_settings()
    {
        // 200ms quiesce 强制收敛 6s 慢 dispose；断线时走 10s 默认，shutdown 要等满 6s dispose 自然完成
        await using var host = new KeystoneHost(Options(
            new KeystoneSettings { QuiesceTimeout = TimeSpan.FromMilliseconds(200) }));
        await host.StartAsync("- id: slowdispose\n  name: ./slowdispose\n");

        var stopwatch = Stopwatch.StartNew();
        await host.ShutdownAsync();
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3),
            $"quiesce 应来自 FrameworkSettings 强制收敛（实际耗时 {stopwatch.Elapsed}）");
    }
}
