namespace Keystone.Runtime.Metrics;

/// <summary>延迟直方图统计（p50/p95，05 §5）。</summary>
public sealed record HistogramStats(double P50, double P95, long Count);

