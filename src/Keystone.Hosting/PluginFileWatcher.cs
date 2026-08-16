namespace Keystone.Hosting;

/// <summary>
/// 插件源文件监听（CA-2，P62，08 §6 第一触发源）：FileSystemWatcher + 防抖合并
///（复用 ConfigFileWatcher 模式）；监听插件源 roots 目录，变更文件回调（含文件名匹配，回调侧决定重载谁）。
/// 回调异常吞掉（watcher 是旁路——报错不崩宿主，记续听）。
/// </summary>
public sealed class PluginFileWatcher : IDisposable
{
    private readonly TimeSpan _debounceDelay;
    private readonly FileSystemWatcher _watcher;
    private readonly Func<string, Task> _onChanged;
    private readonly Lock _lock = new();
    private Timer? _debounce;
    private string? _pendingFile;
    private bool _disposed;

    /// <summary>防抖窗口（P71-T2 入配置面；测试/诊断探针）。</summary>
    internal TimeSpan DebounceDelay => _debounceDelay;

    public PluginFileWatcher(string rootDirectory, Func<string, Task> onChanged, TimeSpan? debounce = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(onChanged);
        if (!Directory.Exists(rootDirectory))
        {
            throw new Keystone.Core.Errors.KeystoneException(
                Keystone.Core.Errors.ErrorCode.ConfigValidationFailed,
                $"plugin watch directory not found: {rootDirectory}");
        }

        _debounceDelay = debounce ?? TimeSpan.FromMilliseconds(100);
        _onChanged = onChanged;
        _watcher = new FileSystemWatcher(rootDirectory)
        {
            IncludeSubdirectories = true, // {root}/{id}/{main} 约定布局（LocalPluginSource 候选路径）
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Renamed += (_, e) => Schedule(e.FullPath);
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e) => Schedule(e.FullPath);

    private void Schedule(string file)
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _pendingFile = file; // 防抖合并：末次变更文件生效
            _debounce?.Dispose();
            _debounce = new Timer(_ => _ = InvokeAsync(), null, _debounceDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private async Task InvokeAsync()
    {
        string? file;
        lock (_lock)
        {
            file = _pendingFile;
            _pendingFile = null;
        }

        if (file is null)
        {
            return;
        }

        try
        {
            await _onChanged(file).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // 重载链可抛任意异常（编译/加载），插件 FAILED 是隔离语义——watcher 旁路续听
        catch (Exception)
        {
            // 本轮重载失败 → 该插件 FAILED（09 §2 隔离），下轮变更重试
        }
#pragma warning restore CA1031
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
            _debounce?.Dispose();
            _debounce = null;
        }

        _watcher.Dispose();
        GC.SuppressFinalize(this);
    }
}
