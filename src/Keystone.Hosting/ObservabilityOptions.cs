namespace Keystone.Hosting;

/// <summary>
/// 观测性配置面（ADR-0018 L3：组合/导出层）。
/// 默认值语义：开发开箱即见（Console 开）——生产经配置切 OTLP、关 Console。
/// 未配置 OTLP 时仅 Console；<see cref="Enabled"/> = false 完全不建 OTel SDK
///（探针层照常工作——功能保底 listener 保证 GetCurrentTaskId 等进程内功能不受影响）。
/// </summary>
public sealed class ObservabilityOptions
{
    /// <summary>总开关：false = 不创建 OTel provider（无导出；探针近零开销）。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Console 导出器（默认开——用户裁定：开发体验开箱即见）。</summary>
    public bool ConsoleEnabled { get; set; } = true;

    /// <summary>OTLP endpoint（如 "http://localhost:4317"）；null = 不启用 OTLP 导出。</summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>采样率（0..1；&lt;1.0 启用 TraceId 比例采样——只影响导出，不影响进程内功能）。</summary>
    public double SampleRatio { get; set; } = 1.0;

    /// <summary>慢请求阈值（P70：无超时调用方永久挂起形态的运行时第一道防线；超阈值告警在 actor 层）。</summary>
    public TimeSpan SlowRequestThreshold { get; set; } = TimeSpan.FromSeconds(5);
}
