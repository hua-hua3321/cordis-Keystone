
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
    /// <summary>占用重试次数默认值（P71-T2 入构造参数——历史硬编码常量）。</summary>
    private const int WriteRetryLimit = 10;

    /// <summary>瞬态拒绝访问退避次数默认值（P2-25；P71-T2 入构造参数）。</summary>
    private const int AccessDeniedRetryLimit = 3;

    /// <summary>重试退避步长默认值（ms，线性递增 (attempt+1)×step；P71-T2 入构造参数）。</summary>
    private const int RetryBackoffStepMs = 50;

    private readonly string _path;
    private readonly int _writeRetryLimit;
    private readonly int _accessDeniedRetryLimit;
    private readonly int _retryBackoffStepMs;
    private readonly TimeSpan _debounceDelay;
    private readonly Lock _lock = new();
    private readonly SemaphoreSlim _writeGate = new(1, 1); // P0-5（19 号审计 IN-3）：写串行化——对齐 Cordis writeQueue 单消费
    private IReadOnlyList<EntryOptions>? _pending;
    private Timer? _debounce;
    private bool _readOnly;

    /// <summary>readonly 状态（CA-7）：拒绝访问后置位——之后所有写静默跳过（08 §6.3 报错不崩溃）。</summary>
    public bool IsReadOnly => Volatile.Read(ref _readOnly);

    /// <summary>readonly 检出回调（CA-7，一次性触发；MA0046 免疫的普通委托属性，嵌入方可接日志/告警）。</summary>
    public Action? OnReadOnly { get; set; }

    /// <param name="writeRetryLimit">共享占用重试次数（默认 10）。</param>
    /// <param name="accessDeniedRetryLimit">瞬态拒绝访问退避次数（默认 3，再降级只读）。</param>
    /// <param name="path">目标配置文件路径。</param>
    /// <param name="debounceDelay">写防抖窗口（默认 50ms）。</param>
    /// <param name="retryBackoffStepMs">重试退避步长（默认 50ms）。</param>
    public ConfigFileWriter(
        string path,
        int writeRetryLimit = WriteRetryLimit,
        int accessDeniedRetryLimit = AccessDeniedRetryLimit,
        TimeSpan? debounceDelay = null,
        int retryBackoffStepMs = RetryBackoffStepMs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfLessThan(writeRetryLimit, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(accessDeniedRetryLimit, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(retryBackoffStepMs, 0);
        _path = path;
        _writeRetryLimit = writeRetryLimit;
        _accessDeniedRetryLimit = accessDeniedRetryLimit;
        _retryBackoffStepMs = retryBackoffStepMs;
        _debounceDelay = debounceDelay ?? TimeSpan.FromMilliseconds(50);
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

    /// <summary>
    /// P2-24（19 号审计 IN-4）：防抖冲刷失败事件——Timer 内 <c>_ = FlushAsync()</c> 的丢弃路径
    /// 经此面暴露（对齐 Cordis logger.warn；显式 FlushAsync/WriteAsync 调用仍直接上抛）。
    /// </summary>
    public event EventHandler<WriteFailedEventArgs>? OnWriteFailed;

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
            _debounce = new Timer(_ => _ = FlushObservedAsync(), null, _debounceDelay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>P2-24：Timer 路径的观察包装——重试耗尽等失败经 <see cref="OnWriteFailed"/> 暴露。</summary>
    private async Task FlushObservedAsync()
    {
        try
        {
            await FlushAsync().ConfigureAwait(false);
        }
        // CA1031：防抖丢弃路径的兜底吞异常——失败经 OnWriteFailed 事件面暴露（P2-4 语义）
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            OnWriteFailed?.Invoke(this, new WriteFailedEventArgs(ex));
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

        _writeGate.Dispose();
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

    /// <summary>原子写入（重试 + 临时文件清理）。
    /// P0-5：全程经 <see cref="_writeGate"/> 串行——Timer 防抖 FlushAsync 与显式
    /// FlushAsync/WriteAsync 并发时不再竞写同一 .tmp（对齐 Cordis writeQueue 链式单消费）。</summary>
    private async Task WriteCoreAsync(string content)
    {
        await _writeGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await WriteSerializedAsync(content).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task WriteSerializedAsync(string content)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await PerformAtomicWriteAsync(_path, content).ConfigureAwait(false);
                return;
            }
            catch (IOException ex) when (IsSharingViolation(ex) && attempt < _writeRetryLimit)
            {
                await Task.Delay((attempt + 1) * _retryBackoffStepMs).ConfigureAwait(false);
            }
            catch (IOException ex) when (attempt >= _writeRetryLimit)
            {
                throw new KeystoneException(
                    ErrorCode.ConfigProviderFailed,
                    $"failed to write config file {_path} after {_writeRetryLimit} attempts: {ex.Message}",
                    ex);
            }
            catch (Exception ex) when (IsAccessDenied(ex) && attempt < _accessDeniedRetryLimit)
            {
                // P2-25（19 号审计 IN-5，对齐 Cordis include 预检+重试）：瞬态拒绝访问
                //（编辑器/杀毒软件短暂占用 ACL）先短退避重试，再判死降级
                await Task.Delay((attempt + 1) * _retryBackoffStepMs).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsAccessDenied(ex))
            {
                // CA-7：拒绝访问 → 只读降级（区别于共享占用：占用该重试、拒绝该降级；
                // Unix EACCES 经 mono 运行时映射到 UnauthorizedAccessException，尽力判定）
                Volatile.Write(ref _readOnly, true);
                OnReadOnly?.Invoke();
                return; // 本次写放弃（静默），后续写直接短路
            }
            catch (Exception ex) when (ex is not KeystoneException)
            {
                // P2-26（19 号审计 IN-6）：意外底层异常统一包 KeystoneException 语义面
                //（修复前 initial 写失败裸 FileNotFoundException/DirectoryNotFoundException 上抛）
                throw new KeystoneException(
                    ErrorCode.ConfigProviderFailed,
                    $"failed to write config file {_path}: {ex.Message}",
                    ex);
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
