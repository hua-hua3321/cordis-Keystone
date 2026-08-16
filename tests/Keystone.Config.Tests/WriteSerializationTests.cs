using Keystone.Config.Entries;
using Keystone.Config.Persistence;

namespace Keystone.Config.Tests;

/// <summary>
/// P0-5（19 号审计 IN-3）：写管线串行化——Cordis writeQueue 链式单消费。
/// 修复前：Timer 防抖 FlushAsync 与显式 FlushAsync/WriteAsync 可并发写同一 .tmp → Move 竞态
/// （一方 tmp 已被另一方 Move 走 → FileNotFoundException/写串文件）。
/// </summary>
public class WriteSerializationTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("keystone-ws-").FullName;
    private readonly List<EntryOptions> _entries = [new() { Id = "a", Name = "./a" }];

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }

        GC.SuppressFinalize(this);
    }

    private sealed class GatedWriter : ConfigFileWriter
    {
        public TaskCompletionSource FirstEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirst { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _entered;

        /// <summary>门闩只拦第一个写（arm 后第一进原子步者停住等放行）。</summary>
        public bool Armed { get; set; }

        public GatedWriter(string path) : base(path)
        {
        }

        protected override async Task PerformAtomicWriteAsync(string targetPath, string content)
        {
            if (Armed && Interlocked.Increment(ref _entered) == 1)
            {
                FirstEntered.TrySetResult(); // 第一个写到达原子步——停住
                await ReleaseFirst.Task.ConfigureAwait(false); // 等测试放行
            }

            await base.PerformAtomicWriteAsync(targetPath, content).ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task Concurrent_writes_are_serialized()
    {
        var path = Path.Combine(_dir, "c.yml");
        using var writer = new GatedWriter(path) { Armed = true };

        var first = writer.WriteAsync(_entries); // 进入原子步并停住
        await writer.FirstEntered.Task; // 确保第一个在写

        var second = writer.WriteAsync(_entries); // 并发第二写（修复前会同时进入原子步）
        await Task.Delay(100); // 若未串行化，第二写早已并发写同一 tmp

        Assert.False(second.IsCompleted); // 串行化：第二写必须等第一个完成

        writer.ReleaseFirst.TrySetResult(); // 放行第一个
        await first; // 第一个完成
        await second; // 第二个接着完成（此时才进入原子步）

        Assert.Equal("- id: a\n  name: ./a", File.ReadAllText(path).TrimEnd()); // 最终内容完整
    }

    [Fact]
    public async Task Scheduled_flush_and_direct_write_do_not_race()
    {
        // Timer 防抖触发与显式 Flush 同一落点的串行性（同锁路径）
        var path = Path.Combine(_dir, "d.yml");
        using var writer = new GatedWriter(path);

        writer.ScheduleWrite(_entries); // 防抖排程（50ms 后 timer 线程 FlushAsync）
        var first = writer.FlushAsync(); // 显式冲刷（取走 pending）
        await first;
        await Task.Delay(80); // 防抖到期触发（pending 已被取走 → 空操作）

        Assert.True(File.Exists(path)); // 写成功（无 tmp 残留竞态）
        Assert.False(File.Exists(path + ".tmp")); // tmp 已被 Move 消费
    }
}
