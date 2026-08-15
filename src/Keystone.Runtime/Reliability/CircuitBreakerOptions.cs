namespace Keystone.Runtime.Reliability;

/// <summary>熔断配置。</summary>
public sealed class CircuitBreakerOptions
{
    /// <summary>连续失败阈值（达到 → Open）。</summary>
    public int FailureThreshold { get; set; } = 3;

    /// <summary>Open 恢复窗口（过后允许 HalfOpen 探测）。</summary>
    public TimeSpan OpenTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

