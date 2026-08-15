using System.Collections.Concurrent;

namespace Keystone.Runtime.Metrics;

/// <summary>
/// 指标注册表（05 §5）：计数器（调用次数/失败）+ 直方图（延迟 p50/p95）。
/// 标签归一化：以 (名称, 标签键, 标签值) 为维度键。
/// </summary>
public sealed class MetricsRegistry
{
    private readonly ConcurrentDictionary<(string Name, string TagKey, string TagValue), double> _counters = [];
    private readonly ConcurrentDictionary<(string Name, string TagKey, string TagValue), List<double>> _histograms = [];
    private readonly Lock _lock = new();

    public void Increment(string name, double value = 1, IReadOnlyDictionary<string, string>? tags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        foreach (var (key, tagValue) in Normalize(tags))
        {
            _counters.AddOrUpdate((name, key, tagValue), value, (_, existing) => existing + value);
        }
    }

    public double GetCounter(string name, string tagKey, string tagValue)
        => _counters.TryGetValue((name, tagKey, tagValue), out var value) ? value : 0;

    public void Record(string name, double value, IReadOnlyDictionary<string, string>? tags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        foreach (var (key, tagValue) in Normalize(tags))
        {
            lock (_lock)
            {
                var list = _histograms.GetOrAdd((name, key, tagValue), _ => []);
                list.Add(value);
            }
        }
    }

    public HistogramStats GetHistogram(string name, string tagKey, string tagValue)
    {
        lock (_lock)
        {
            if (!_histograms.TryGetValue((name, tagKey, tagValue), out var values))
            {
                return new HistogramStats(0, 0, 0);
            }

            var sorted = values.OrderBy(v => v).ToArray();
            return new HistogramStats(
                Percentile(sorted, 0.50),
                Percentile(sorted, 0.95),
                sorted.Length);
        }
    }

    private static double Percentile(double[] sorted, double p)
    {
        if (sorted.Length == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling(p * sorted.Length) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Length - 1))];
    }

    private static IEnumerable<KeyValuePair<string, string>> Normalize(IReadOnlyDictionary<string, string>? tags)
        => tags is { Count: > 0 }
            ? tags.OrderBy(kv => kv.Key, StringComparer.Ordinal)
            : [new KeyValuePair<string, string>("_", "_")];
}
