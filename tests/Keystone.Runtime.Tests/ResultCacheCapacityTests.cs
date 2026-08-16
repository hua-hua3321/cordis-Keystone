using Keystone.Core.Contracts;
using Keystone.Runtime.Actors;

namespace Keystone.Runtime.Tests;

/// <summary>
/// P71-T2（硬编码审计批）：actor 结果缓存容量（DC-13 幂等去重的 FIFO 上限）入参数。
/// 修复前：CapabilityActor.ResultCacheCapacity 私有常量 1024——内存敏感部署无法调小。
/// </summary>
public class ResultCacheCapacityTests
{
    [Fact]
    public async Task Capacity_evicts_oldest_and_reexecutes()
    {
        await using var domain = CapabilityDomain.Create("cache", resultCacheCapacity: 2);
        var executions = 0;
        var handle = domain.Spawn("c", e =>
        {
            Interlocked.Increment(ref executions);
            return Task.FromResult(new TaskResultEnvelope
            {
                TaskId = e.TaskId,
                Succeeded = true,
                Type = TaskResultType.Completed,
            });
        });

        var ids = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();
        foreach (var id in ids)
        {
            await domain.RequestAsync(handle, Envelope(id), CancellationToken.None);
        }

        Assert.Equal(3, executions); // 三个不同 TaskId 各执行一次

        var replay = await domain.RequestAsync(handle, Envelope(ids[0]), CancellationToken.None);
        Assert.True(replay.Succeeded);
        Assert.Equal(4, executions); // 容量 2 → task1 已淘汰 → 重执行（默认 1024 会命中缓存，count 停在 3）
    }

    private static TaskEnvelope Envelope(Guid taskId) => new()
    {
        TaskId = taskId,
        Capability = "cache",
        Operation = "read",
        PayloadBytes = [],
    };
}
