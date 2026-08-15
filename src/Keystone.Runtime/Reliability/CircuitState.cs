namespace Keystone.Runtime.Reliability;

/// <summary>熔断状态（05 §3）：Closed → Open（连续失败）→ HalfOpen（恢复窗口后探测）→ Closed/Open。</summary>
public enum CircuitState
{
    Closed,
    Open,
    HalfOpen,
}

