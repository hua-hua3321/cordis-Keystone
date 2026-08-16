using Keystone.Config.Entries;
using Keystone.Config.Persistence;
using Keystone.Core.Errors;

namespace Keystone.Config.Tests;

/// <summary>
/// P71-T2（硬编码审计批）：写回管线可调值入构造参数。
/// 修复前：占用重试次数（10）/ 拒绝访问重试（3）/ 防抖窗口（50ms）/ 退避步长（50ms）
/// 全部为私有常量——网络盘/杀软扫描慢的机器、高频写回场景无法调整。
/// </summary>
public class WriterOptionsTests
{
    private static IReadOnlyList<EntryOptions> Entries()
        => [new() { Id = "a", Name = "./a" }];

    private sealed class AlwaysSharingViolationWriter(string path)
        : ConfigFileWriter(path, writeRetryLimit: 2, retryBackoffStepMs: 1)
    {
        public int WriteAttempts { get; private set; }

        protected override Task PerformAtomicWriteAsync(string targetPath, string content)
        {
            WriteAttempts++;
            throw new IOException("sharing violation", hresult: unchecked((int)0x80070020));
        }
    }

    [Fact]
    public async Task Retry_limit_and_backoff_step_are_configurable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"keystone-writer-opts-{Guid.NewGuid():N}.yaml");
        using var writer = new AlwaysSharingViolationWriter(path);
        var failures = new List<Exception>();
        writer.OnWriteFailed += (_, e) => failures.Add(e.Exception);

        writer.ScheduleWrite(Entries());
        await WaitForAsync(() => failures.Count > 0);

        // 修复前固定走 const 10：消息为 "after 10 attempts"、WriteAttempts == 11
        var failure = Assert.IsType<KeystoneException>(failures[0]);
        Assert.Contains("after 2 attempts", failure.Message);
        Assert.Equal(3, writer.WriteAttempts); // 2 次重试 + 第 3 次放弃上抛
    }

    [Fact]
    public async Task Debounce_delay_is_configurable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"keystone-writer-deb-{Guid.NewGuid():N}.yaml");
        using var writer = new ConfigFileWriter(path, debounceDelay: TimeSpan.FromMilliseconds(1));

        writer.ScheduleWrite(Entries());
        await WaitForAsync(() => File.Exists(path));

        Assert.True(File.Exists(path), "1ms 防抖应立即落盘（参数生效）");
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
