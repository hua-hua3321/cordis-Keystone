namespace Keystone.Hosting;

/// <summary>配置写回管线可调值（P71-T2 硬编码入配置面；对应 ConfigFileWriter 构造参数）。</summary>
public sealed class ConfigWriterOptions
{
    /// <summary>共享占用重试次数上限（默认 10——网络盘/杀软扫描慢的机器可加大）。</summary>
    public int WriteRetryLimit { get; set; } = 10;

    /// <summary>瞬态拒绝访问短退避次数（默认 3；再降级只读模式，CA-7）。</summary>
    public int AccessDeniedRetryLimit { get; set; } = 3;

    /// <summary>写防抖窗口（默认 50ms——高频写回场景调大以合并更多）。</summary>
    public TimeSpan DebounceDelay { get; set; } = TimeSpan.FromMilliseconds(50);

    /// <summary>重试退避步长（默认 50ms，线性递增 (attempt+1)×step）。</summary>
    public int RetryBackoffStepMs { get; set; } = 50;
}
