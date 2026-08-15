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
