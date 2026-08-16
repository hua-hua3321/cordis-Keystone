
using Keystone.Config.Entries;
using Keystone.Core.Errors;

namespace Keystone.Config.Persistence;

/// <summary>
/// 配置写回管线（F6，对齐 Cordis plugin-include）：原子写（tmp + File.Move 覆盖替换）、
/// 占用重试（IOException HRESULT 0x80070020 退避）、写防抖合并、initial 引导、
/// readonly 优雅降级（CA-7：拒绝访问 0x80070005 → 只读模式，后续写静默跳过不抛——08 §6.3）。
/// </summary>
public class ConfigFileWriter : IDisposable
{
    private const int WriteRetryLimit = 10;
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(50);

    private readonly string _path;
    private readonly Lock _lock = new();
    private IReadOnlyList<EntryOptions>? _pending;
    private Timer? _debounce;
    private bool _readOnly;

    /// <summary>readonly 状态（CA-7）：拒绝访问后置位——之后所有写静默跳过（08 §6.3 报错不崩溃）。</summary>
    public bool IsReadOnly => Volatile.Read(ref _readOnly);

    /// <summary>readonly 检出回调（CA-7，一次性触发；MA0046 免疫的普通委托属性，嵌入方可接日志/告警）。</summary>
    public Action? OnReadOnly { get; set; }

    public ConfigFileWriter(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    /// <summary>原子写入（重试 + 临时文件清理）。</summary>
    public Task WriteAsync(IReadOnlyList<EntryOptions> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (Volatile.Read(ref _readOnly))
        {
            return Task.CompletedTask; // CA-7：readonly 静默跳过（不抛不尝试）
        }

        var content = EntrySerializer.Serialize(entries);
        return WriteCoreAsync(content);
    }

    /// <summary>防抖调度写（多次变更合并为一次，F6）。</summary>
    public void ScheduleWrite(IReadOnlyList<EntryOptions> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (Volatile.Read(ref _readOnly))
        {
            return; // CA-7：readonly 静默跳过
        }

        lock (_lock)
        {
            _pending = entries;
            _debounce?.Dispose();
            _debounce = new Timer(_ => _ = FlushAsync(), null, DebounceDelay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>立即冲刷防抖队列（测试/关闭前调用）。</summary>
    public async Task FlushAsync()
    {
        if (Volatile.Read(ref _readOnly))
        {
            return; // CA-7：readonly 静默跳过
        }

        IReadOnlyList<EntryOptions>? pending;
        lock (_lock)
        {
            _debounce?.Dispose();
            _debounce = null;
            pending = _pending;
            _pending = null;
        }

        if (pending is not null)
        {
            await WriteAsync(pending).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _debounce?.Dispose();
            _debounce = null;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>initial 引导（F6）：文件不存在 → 写初始配置；存在 → 跳过。</summary>
    public async Task EnsureInitialAsync(IReadOnlyList<EntryOptions> initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        if (File.Exists(_path))
        {
            return;
        }

        await WriteAsync(initial).ConfigureAwait(false);
    }

    private async Task WriteCoreAsync(string content)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await PerformAtomicWriteAsync(_path, content).ConfigureAwait(false);
                return;
            }
            catch (IOException ex) when (IsSharingViolation(ex) && attempt < WriteRetryLimit)
            {
                await Task.Delay((attempt + 1) * 50).ConfigureAwait(false);
            }
            catch (IOException ex) when (attempt >= WriteRetryLimit)
            {
                throw new KeystoneException(
                    ErrorCode.ConfigProviderFailed,
                    $"failed to write config file {_path} after {WriteRetryLimit} attempts: {ex.Message}",
                    ex);
            }
            catch (Exception ex) when (IsAccessDenied(ex))
            {
                // CA-7：拒绝访问 → 只读降级（区别于共享占用：占用该重试、拒绝该降级；
                // Unix EACCES 经 mono 运行时映射到 UnauthorizedAccessException，尽力判定）
                Volatile.Write(ref _readOnly, true);
                OnReadOnly?.Invoke();
                return; // 本次写放弃（静默），后续写直接短路
            }
        }
    }

    private static bool IsAccessDenied(Exception ex)
        => ex is UnauthorizedAccessException
           || ex.HResult is unchecked((int)0x80070005);

    private static bool IsSharingViolation(IOException ex)
        => ex.HResult is unchecked((int)0x80070020);

    /// <summary>原子写（tmp + Move 覆盖替换；测试可覆盖注入故障）。</summary>
    protected virtual async Task PerformAtomicWriteAsync(string targetPath, string content)
    {
        var tmp = targetPath + ".tmp";
        await File.WriteAllTextAsync(tmp, content).ConfigureAwait(false);
        File.Move(tmp, targetPath, overwrite: true); // 同卷原子替换（对齐 tmp+rename，F6）
    }

}
