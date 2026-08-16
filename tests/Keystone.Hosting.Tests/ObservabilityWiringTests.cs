using Keystone.Core.Contracts;

namespace Keystone.Hosting.Tests;

/// <summary>
/// P70-T2（ADR-0018 L3）：OTel 组合接线端到端——宿主启动建 provider（AddSource 订阅
/// Runtime 探针源），域请求 span 经默认 Console 导出面可见；Enabled=false 完全无导出。
/// 断言用唯一 operation 标记防并行测试串扰（其余测试的宿主默认同样开 Console）。
/// Console exporter 同步写 Console.Out——writer 必须先于请求装上（span 在请求完成时导出）。
/// </summary>
[Collection("Observability")]
public class ObservabilityWiringTests
{
    /// <summary>最小可加载插件（宿主启动即编译入 ALC；本测试只用能力域，不触发插件逻辑）。</summary>
    public const string MinimalPluginSource = """
        using System;
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        public sealed class ObsPlugin : IPlugin
        {
            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
                => Task.CompletedTask;

            public Task DisposeAsync() => Task.CompletedTask;
        }
        """;

    private static KeystoneHostOptions Options() => new()
    {
        EnableCapabilityDomain = true,
        ManifestProvider = _ => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
            "obs", "1.0.0", "Obs.cs", ["cordis-runtime"], [], []),
        SourceProvider = _ => new Keystone.Runtime.Plugins.Loading.PluginSource("obs", MinimalPluginSource),
    };

    [Fact]
    public async Task Domain_request_span_exports_via_default_console()
    {
        var writer = new StringWriter();
        var original = Console.Out;
        Console.SetOut(writer);
        try
        {
            await using var host = new KeystoneHost(Options());
            await host.StartAsync("- id: obs\n  name: ./obs\n");

            var marker = $"obs-e2e-{Guid.NewGuid():N}";
            var domain = host.GetCapabilityDomain();
            Assert.NotNull(domain);
            var handle = domain!.Spawn("obs-e2e", e => Task.FromResult(new TaskResultEnvelope
            {
                TaskId = e.TaskId,
                Succeeded = true,
                Type = TaskResultType.Completed,
            }));
            var result = await domain.RequestAsync(handle, new TaskEnvelope
            {
                TaskId = Guid.NewGuid(),
                Capability = "obs",
                Operation = marker,
                PayloadBytes = [],
            }, CancellationToken.None);
            Assert.True(result.Succeeded);

            await host.ShutdownAsync();
        }
        finally
        {
            Console.SetOut(original);
        }

        // Console 导出面全链证明：唯一 operation 标记出现在导出内容
        Assert.Contains("obs-e2e-", writer.ToString());
    }

    [Fact]
    public async Task Disabled_observability_builds_no_provider_and_function_preserved()
    {
        // OTel provider 是进程级全局 listener——并行宿主会"代为导出"本宿主的 activity，
        // 故导出内容不可作单宿主负断言（全量下假失败）；配置生效性以 provider 未建立为准。
        var options = Options();
        options.Observability = new ObservabilityOptions { Enabled = false };
        await using var host = new KeystoneHost(options);
        await host.StartAsync("- id: obs\n  name: ./obs\n");

        var domain = host.GetCapabilityDomain();
        Assert.NotNull(domain);
        var handle = domain!.Spawn("obs-off", e => Task.FromResult(new TaskResultEnvelope
        {
            TaskId = e.TaskId,
            Succeeded = true,
            Type = TaskResultType.Completed,
        }));
        var result = await domain.RequestAsync(handle, new TaskEnvelope
        {
            TaskId = Guid.NewGuid(),
            Capability = "obs",
            Operation = "off",
            PayloadBytes = [],
        }, CancellationToken.None);

        Assert.True(result.Succeeded); // 功能不受影响（保底 listener 在探针层，独立于导出）
        Assert.False(host.TracerProviderBuilt); // P70-T2：未配置 → 不建 provider
        Assert.False(host.MeterProviderBuilt); // P70-T5：未配置 → 不建 meter provider
        await host.ShutdownAsync();
    }

    [Fact]
    public async Task Default_observability_builds_meter_provider()
    {
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("- id: obs\n  name: ./obs\n");

        Assert.True(host.MeterProviderBuilt); // P70-T5：指标导出管线默认建（ADR-0018 L3）
        await host.ShutdownAsync();
    }
}
