using Keystone.Core.Contracts;
using Keystone.Runtime.Actors;
using Proto;

namespace Keystone.Runtime.Tests;

/// <summary>
/// DC-4 监督策略测试（17-doc-compliance-audit，05 §2/09 §3）：
/// OneForOne 崩溃重启 + 连续失败超阈值 → 停止重启（域不可用升级语义）。
/// </summary>
public class SupervisionPolicyTests
{
    [Fact]
    public async Task Transient_failure_restarts_actor()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var system = new ActorSystem();
        var domain = CapabilityDomain.Attach(system, "fs");
        var calls = 0;
        var handle = domain.Spawn("fs-a", async envelope =>
        {
            Interlocked.Increment(ref calls);
            if (calls == 1)
            {
                throw new InvalidOperationException("transient");
            }

            return new TaskResultEnvelope { TaskId = envelope.TaskId, Succeeded = true, Type = TaskResultType.Completed };
        });

        // P68 监督观测面更新：handler 崩溃 → 立即失败结果回填（不挂死）+ actor 重启
        using var firstCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var first = await domain.RequestAsync(handle, Envelope("1"), firstCts.Token);
        Assert.False(first.Succeeded); // 崩溃即时回填（修复前挂到调用方超时）
        Assert.Equal(Keystone.Core.Errors.ErrorCode.PipelineExecutionFailed, first.ErrorCode);

        using var secondCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var result = await domain.RequestAsync(handle, Envelope("2"), secondCts.Token);
        Assert.True(result.Succeeded); // 重启后成功（OneForOne Restart——respond 后上抛触发）
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Repeated_failures_beyond_threshold_stop_restarting()
    {
        // MaxRestarts=1：两次连续失败后 actor 被停止（域不可用升级），后续请求失败而非无限重启
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var system = new ActorSystem();
        var domain = CapabilityDomain.Attach(system, "fs");
        var calls = 0;
        var handle = domain.Spawn(
            "fs-a",
            envelope =>
            {
                Interlocked.Increment(ref calls);
                throw new InvalidOperationException("always-fails");
            },
            supervision: new CapabilitySupervisionOptions { MaxRestarts = 1, RestartWindow = TimeSpan.FromSeconds(5) });

        // 首次失败 → 重启；二次失败 → 超阈值停止
        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await domain.RequestAsync(handle, Envelope("1"), cts1.Token); } catch { }
        await Task.Delay(100);
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await domain.RequestAsync(handle, Envelope("2"), cts2.Token); } catch { }
        await Task.Delay(100);

        // 第三次：actor 已停止（域不可用）→ 请求失败（不再重启）
        var callsBefore = calls;
        using var cts3 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var failed = false;
        try
        {
            await domain.RequestAsync(handle, Envelope("3"), cts3.Token);
        }
        catch (Exception)
        {
            failed = true; // 停止后请求失败（超阈值不再重启）
        }

        Assert.True(failed);
        Assert.Equal(callsBefore, calls); // 第三次未再执行 handler（未重启）
    }

    private static TaskEnvelope Envelope(string op) => new()
    {
        TaskId = Guid.NewGuid(),
        Capability = "fs",
        Operation = op,
        PayloadBytes = [],
    };
}
