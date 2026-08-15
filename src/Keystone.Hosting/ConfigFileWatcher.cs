namespace Keystone.Hosting;

/// <summary>
/// 配置文件监听（DC-9，08 §6 触发源）：FileSystemWatcher + 防抖合并
/// （多次变更合并一次回调——next-tick 级合并，08 §6.3 写防抖同语义）。
/// 回调异常吞掉（watcher 是旁路——报错不崩宿主，记续听）。
/// </summary>
public sealed class ConfigFileWatcher : IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(100);

    private readonly FileSystemWatcher _watcher;
    private readonly Func<Task> _onChanged;
    private readonly Lock _lock = new();
    private Timer? _debounce;
    private bool _disposed;

    public ConfigFileWatcher(string path, Func<Task> onChanged)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(onChanged);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is null || !Directory.Exists(directory))
        {
            throw new Keystone.Core.Errors.KeystoneException(
                Keystone.Core.Errors.ErrorCode.ConfigValidationFailed,
                $"config watch directory not found: {path}");
        }

        _onChanged = onChanged;
        _watcher = new FileSystemWatcher(directory)
        {
            Filter = Path.GetFileName(path),
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += (_, _) => Schedule();
        _watcher.Created += (_, _) => Schedule();
    }

    private void Schedule()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _debounce?.Dispose();
            _debounce = new Timer(_ => _ = InvokeAsync(), null, Debounce, Timeout.InfiniteTimeSpan);
        }
    }

    private async Task InvokeAsync()
    {
        try
        {
            await _onChanged().ConfigureAwait(false);
        }
#pragma warning disable CA1031 // 应用链可抛任意异常（解析/校验/加载），保留旧树续听是旁路降级语义
        catch (Exception)
        {
            // 本轮应用失败保留旧树（08 §6 失败保留旧配置），下轮变更重试
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
