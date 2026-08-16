using Keystone.Config.Entries;
using Keystone.Config.Persistence;

namespace Keystone.Config.Tests;

public class ConfigFileWriterTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("keystone-writer-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Write_creates_file_with_atomic_replace()
    {
        var path = Path.Combine(_directory, "keystone.yml");
        var writer = new ConfigFileWriter(path);
        var entries = EntryParser.Parse("- id: fs\n  name: ./plugins/fs\n");

        await writer.WriteAsync(entries);

        Assert.True(File.Exists(path));
        var reloaded = EntryParser.Parse(File.ReadAllText(path));
        Assert.Equal("fs", Assert.Single(reloaded).Id);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp")); // 临时文件已清理
    }

    [Fact]
    public async Task Initial_bootstrap_writes_when_file_missing()
    {
        var path = Path.Combine(_directory, "keystone.yml");
        var writer = new ConfigFileWriter(path);
        var initial = EntryParser.Parse("- id: fs\n  name: ./plugins/fs\n");

        await writer.EnsureInitialAsync(initial);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Initial_bootstrap_skips_when_file_exists()
    {
        var path = Path.Combine(_directory, "keystone.yml");
        File.WriteAllText(path, "- id: existing\n  name: ./plugins/existing\n");
        var writer = new ConfigFileWriter(path);

        await writer.EnsureInitialAsync(EntryParser.Parse("- id: fs\n  name: ./plugins/fs\n"));

        Assert.Contains("existing", File.ReadAllText(path)); // 未覆盖已有文件
    }

    [Fact]
    public async Task Debounced_writes_coalesce_into_single_write()
    {
        var path = Path.Combine(_directory, "keystone.yml");
        var writer = new ConfigFileWriter(path);
        var entries = EntryParser.Parse("- id: fs\n  name: ./plugins/fs\n");

        writer.ScheduleWrite(entries);
        writer.ScheduleWrite(entries);
        writer.ScheduleWrite(entries);
        await writer.FlushAsync();

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Retry_on_transient_io_error()
    {
        var path = Path.Combine(_directory, "keystone.yml");
        var writer = new FlakyPathConfigFileWriter(path, failFirstWrite: true);
        var entries = EntryParser.Parse("- id: fs\n  name: ./plugins/fs\n");

        await writer.WriteAsync(entries); // 首次写入抛 IOException（模拟占用），重试成功

        Assert.True(File.Exists(path));
        Assert.Equal(2, writer.WriteAttempts);
    }

    [Fact]
    public async Task Access_denied_marks_readonly_and_swallows_subsequent_writes()
    {
        // CA-7（P61）readonly 优雅降级：0x80070005（拒绝访问）→ 置 readonly + 回调恰一次；
        // 之后写静默跳过（不抛、不再尝试——对齐 include checkAccess(W_OK) 预检降级）
        var path = Path.Combine(_directory, "ro.yml");
        var detected = 0;
        using var writer = new AccessDeniedWriter(path) { OnReadOnly = () => detected++ };

        await writer.WriteAsync(EntryParser.Parse("- id: a\n  name: ./a\n")); // 首写触发降级
        Assert.True(writer.IsReadOnly);
        Assert.Equal(1, detected); // 回调恰一次

        await writer.WriteAsync(EntryParser.Parse("- id: b\n  name: ./b\n")); // 静默返回（不抛）
        Assert.True(writer.IsReadOnly); // 仍 readonly
        Assert.False(File.Exists(path)); // 从未真正写盘
    }

    [Fact]
    public async Task Sharing_violation_still_retries_without_readonly()
    {
        // CA-7：0x80070020（共享占用）≠ 拒绝访问——该重试不降级
        var path = Path.Combine(_directory, "busy.yml");
        using var writer = new SharingViolationWriter(path);

        // 占用 2 次后放行 → 重试成功（未降级）
        await writer.WriteAsync(EntryParser.Parse("- id: a\n  name: ./a\n"));

        Assert.False(writer.IsReadOnly);
        Assert.True(File.Exists(path)); // 重试后写成功
    }

    private sealed class AccessDeniedWriter(string path) : ConfigFileWriter(path)
    {
        protected override Task PerformAtomicWriteAsync(string targetPath, string content)
            => throw new UnauthorizedAccessException("access denied") { HResult = unchecked((int)0x80070005) };
    }

    private sealed class SharingViolationWriter(string path) : ConfigFileWriter(path)
    {
        private int _attempts;

        protected override async Task PerformAtomicWriteAsync(string targetPath, string content)
        {
            if (_attempts++ < 2)
            {
                throw new IOException("in use", unchecked((int)0x80070020));
            }

            await base.PerformAtomicWriteAsync(targetPath, content);
        }
    }

    private sealed class FlakyPathConfigFileWriter : ConfigFileWriter
    {
        public int WriteAttempts { get; private set; }

        private readonly bool _failFirst;

        public FlakyPathConfigFileWriter(string path, bool failFirstWrite)
            : base(path)
        {
            _failFirst = failFirstWrite;
        }

        protected override async Task PerformAtomicWriteAsync(string targetPath, string content)
        {
            WriteAttempts++;
            if (_failFirst && WriteAttempts == 1)
            {
                throw new IOException("sharing violation", hresult: unchecked((int)0x80070020));
            }

            await base.PerformAtomicWriteAsync(targetPath, content);
        }
    }
}
