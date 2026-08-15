using Keystone.Core.Contracts;
using Keystone.Runtime.Actors;
using Keystone.Runtime.Context;
using Keystone.Runtime.Pipeline;
using Proto;

namespace Keystone.Runtime.Tests;

/// <summary>
/// DC-10（17-doc-compliance-audit / ADR-0003 决策 2 / 04 §8）：管道原子替换（swap）。
/// 修复前：管道每请求重建（PipelineBuilder per request），节点 spawn 固化，无 swap API。
/// 兑现：管道实例化缓存 + SwapPipeline 原子替换——保留 actor/context，只换管道链；
/// 在途请求走旧管道（消息串行，swap 消息到达时在途请求已完成），新请求走新管道。
/// </summary>
public class PipelineSwapTests
{
    private sealed class RecordingMiddleware(string id, List<string> trace) : IMiddleware
    {
        public string Id => id;

        public int Order => 0;

        public async Task InvokeAsync(IPluginContext ctx, RequestDelegate next)
        {
            trace.Add($"{id}:before");
            await next(ctx);
            trace.Add($"{id}:after");
        }
    }

    private static TaskEnvelope Envelope(Guid taskId) => new()
    {
        TaskId = taskId,
        Capability = "fs",
        Operation = "read",
        PayloadBytes = [],
    };

    private static TaskResultEnvelope Ok(TaskEnvelope e) => new()
    {
        TaskId = e.TaskId,
        Succeeded = true,
        Type = TaskResultType.Completed,
    };

    [Fact]
    public async Task Swap_replaces_middleware_chain_atomically()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var system = new ActorSystem();
        var domain = CapabilityDomain.Attach(system, "fs");
        var trace = new List<string>();

        var handle = domain.Spawn("fs-a", e => Task.FromResult(Ok(e)),
            new IMiddleware[] { new RecordingMiddleware("old", trace) });

        await domain.RequestAsync(handle, Envelope(Guid.NewGuid()), cts.Token);
        Assert.Equal(["old:before", "old:after"], trace);

        // swap：旧链 → 新链（原子替换，保留 actor/context）
        await domain.SwapPipelineAsync(handle, [new RecordingMiddleware("new", trace)]);

        trace.Clear();
        await domain.RequestAsync(handle, Envelope(Guid.NewGuid()), cts.Token);
        Assert.Equal(["new:before", "new:after"], trace); // 新请求全走新链
    }

    [Fact]
    public async Task Swap_preserves_instance_context_state()
    {
        // ADR-0003 决策 2：保留 context（状态不丢）——swap 只换管道链
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var system = new ActorSystem();
        var domain = CapabilityDomain.Attach(system, "fs");
        var trace = new List<string>();

        // 计数中间件：经 ctx.Provide 写入实例 context 状态（DC-1 持久 context 跨请求累积）
        var counting = new CountingMiddleware(trace);
        var handle = domain.Spawn("fs-a", e => Task.FromResult(Ok(e)), [counting]);

        await domain.RequestAsync(handle, Envelope(Guid.NewGuid()), cts.Token);
        await domain.SwapPipelineAsync(handle, [new CountingMiddleware(trace)]);
        await domain.RequestAsync(handle, Envelope(Guid.NewGuid()), cts.Token);
        await domain.RequestAsync(handle, Envelope(Guid.NewGuid()), cts.Token);

        // 计数经实例 context 跨请求累积：swap 后第二条中间件读到 1/2（而非 0）→ context 未随管道替换重建
        Assert.Equal(["observed=0", "observed=1", "observed=2"], trace);
    }

    private sealed class CountingMiddleware(List<string> trace) : IMiddleware
    {
        public string Id => "counter";

        public int Order => 0;

        public async Task InvokeAsync(IPluginContext ctx, RequestDelegate next)
        {
            var current = ctx.TryGet<int>("request-count");
            trace.Add($"observed={current}");
            ctx.Provide("request-count", current + 1); // 写公共祖先（实例 root，03 §2.1）
            await next(ctx);
        }
    }

    [Fact]
    public async Task Pipeline_is_cached_not_rebuilt_per_request()
    {
        // 管道实例化缓存：同一链的多次请求复用同一管道实例（构建计数 = 1 次/swap）
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var system = new ActorSystem();
        var domain = CapabilityDomain.Attach(system, "fs");

        var handle = domain.Spawn("fs-a", e => Task.FromResult(Ok(e)),
            [new BuildCountingMiddleware()]);

        for (var i = 0; i < 3; i++)
        {
            await domain.RequestAsync(handle, Envelope(Guid.NewGuid()), cts.Token);
        }

        Assert.Equal(1, BuildCountingMiddleware.BuildCount); // 3 请求仅 1 次构建（缓存）
    }

    private sealed class BuildCountingMiddleware : IMiddleware
    {
        public static int BuildCount;

        public BuildCountingMiddleware() => BuildCount++;

        public string Id => "build-counter";

        public int Order => 0;

        public Task InvokeAsync(IPluginContext ctx, RequestDelegate next) => next(ctx);
    }
}
