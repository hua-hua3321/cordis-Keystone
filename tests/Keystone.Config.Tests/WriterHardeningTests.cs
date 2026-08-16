using Keystone.Config.Entries;
using Keystone.Config.Persistence;
using Keystone.Core.Errors;

namespace Keystone.Config.Tests;

/// <summary>
/// P68（19 号审计 P2-24 + P2-26）：
/// P2-24 防抖 FlushAsync 异常未观察（Timer 里 `_ = FlushAsync()` 丢弃——重试耗尽抛的
/// KeystoneException 成 unobserved 静默丢失；Cordis 有 logger.warn）→ OnWriteFailed 事件面；
/// P2-26 initial 写失败裸异常（FileNotFoundException/DirectoryNotFoundException 未包
/// KeystoneException 语义）→ 统一包装。
/// </summary>
public class WriterHardeningTests
{
    private static IReadOnlyList<EntryOptions> Entries() =>
        [new() { Id = "a", Name = "./a" }];

    [Fact]
    public async Task Debounced_flush_failure_surfaces_via_OnWriteFailed()
    {
        // 目标路径的父目录不存在 → 原子写失败（目录缺失）
        var path = Path.Combine(Path.GetTempPath(), $"keystone-missing-{Guid.NewGuid():N}", "cfg.yaml");
        using var writer = new ConfigFileWriter(path);
        var failures = new List<Exception>();
        writer.OnWriteFailed += (_, e) => failures.Add(e.Exception);

        writer.ScheduleWrite(Entries()); // 防抖调度（Timer 内 FlushAsync——修复前异常被丢弃）
        await WaitForAsync(() => failures.Count > 0);

        Assert.NotEmpty(failures); // 修复前静默丢失
        Assert.IsType<KeystoneException>(failures[0]); // P2-26：包装语义
    }

    [Fact]
    public async Task Initial_write_failure_wrapped_in_keystone_exception()
    {
        var path = Path.Combine(Path.GetTempPath(), $"keystone-missing-{Guid.NewGuid():N}", "cfg.yaml");
        using var writer = new ConfigFileWriter(path);

        // 修复前裸 DirectoryNotFoundException（非 Keystone 语义面）
        var exception = await Assert.ThrowsAsync<KeystoneException>(
            () => writer.EnsureInitialAsync(Entries()));
        Assert.Equal(ErrorCode.ConfigProviderFailed, exception.Code);
    }

    private static async Task WaitForAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                Assert.Fail("condition not met within timeout");
            }

            await Task.Delay(20);
        }
    }
}
