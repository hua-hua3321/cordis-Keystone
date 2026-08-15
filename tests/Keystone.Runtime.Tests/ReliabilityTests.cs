using Keystone.Runtime.Logging;
using Keystone.Runtime.Metrics;
using Keystone.Runtime.Reliability;
using Keystone.Core.Errors;
using Microsoft.Extensions.Logging;

namespace Keystone.Runtime.Tests;

public class RingBufferLoggerProviderTests
{
    [Fact]
    public void Logs_are_captured_in_ring_buffer()
    {
        var provider = new RingBufferLoggerProvider(1000);
        var logger = provider.CreateLogger("fs/read");

        logger.LogInformation("task {TaskId} phase {Phase}", "t1", "before");

        var snapshot = provider.GetSnapshot();
        var record = Assert.Single(snapshot);
        Assert.Equal("fs/read", record.Category);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal("task t1 phase before", record.Message);
    }

    [Fact]
    public void Ring_buffer_caps_at_capacity()
    {
        var provider = new RingBufferLoggerProvider(3);
        var logger = provider.CreateLogger("fs");

        for (var i = 0; i < 10; i++)
        {
            logger.LogInformation("entry {N}", i);
        }

        var snapshot = provider.GetSnapshot();
        Assert.Equal(3, snapshot.Count); // 环形：只留最近 3 条
        Assert.Equal("entry 9", snapshot[^1].Message);
    }

    [Fact]
    public void Exception_parameter_is_expanded_into_message()
    {
        var provider = new RingBufferLoggerProvider(1000);
        var logger = provider.CreateLogger("fs");

        logger.LogError(new InvalidOperationException("boom"), "failed {Op}", "read");

        var record = Assert.Single(provider.GetSnapshot());
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Equal("failed read", record.Message);
        Assert.IsType<InvalidOperationException>(record.Exception); // L4：异常为结构化字段
    }
}

public class MetricsRegistryTests
{
    [Fact]
    public void Increment_accumulates_counter()
    {
        var metrics = new MetricsRegistry();
        metrics.Increment("plugin.calls", tags: new Dictionary<string, string> { ["plugin"] = "fs" });
        metrics.Increment("plugin.calls", tags: new Dictionary<string, string> { ["plugin"] = "fs" });
        metrics.Increment("plugin.calls", tags: new Dictionary<string, string> { ["plugin"] = "llm" });

        Assert.Equal(2, metrics.GetCounter("plugin.calls", "plugin", "fs"));
        Assert.Equal(1, metrics.GetCounter("plugin.calls", "plugin", "llm"));
    }

    [Fact]
    public void Record_computes_latency_percentiles()
    {
        var metrics = new MetricsRegistry();
        for (var i = 1; i <= 100; i++)
        {
            metrics.Record("pipeline.duration", i, tags: new Dictionary<string, string> { ["domain"] = "fs" });
        }

        var latency = metrics.GetHistogram("pipeline.duration", "domain", "fs");

        Assert.Equal(50, latency.P50);
        Assert.Equal(95, latency.P95);
    }
}

public class CircuitBreakerTests
{
    private static CircuitBreaker CreateBreaker(int failureThreshold = 3, TimeSpan? openTimeout = null)
        => new(new CircuitBreakerOptions
        {
            FailureThreshold = failureThreshold,
            OpenTimeout = openTimeout ?? TimeSpan.FromMilliseconds(100),
        });

    [Fact]
    public async Task Opens_after_consecutive_failures_and_rejects()
    {
        var breaker = CreateBreaker(failureThreshold: 2);
        var attempts = 0;

        for (var i = 0; i < 2; i++)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                breaker.ExecuteAsync<string>(_ => throw new InvalidOperationException("fail"), CancellationToken.None));
        }

        Assert.Equal(CircuitState.Open, breaker.State);

        // Open：快速失败（熔断拒绝）
        await Assert.ThrowsAsync<KeystoneException>(() =>
            breaker.ExecuteAsync<string>(_ =>
            {
                attempts++;
                return Task.FromResult("should not run");
            }, CancellationToken.None));
        Assert.Equal(0, attempts);
    }

    [Fact]
    public async Task Half_open_probe_success_closes_circuit()
    {
        var breaker = CreateBreaker(failureThreshold: 1, openTimeout: TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            breaker.ExecuteAsync<string>(_ => throw new InvalidOperationException("fail"), CancellationToken.None));
        Assert.Equal(CircuitState.Open, breaker.State);

        await Task.Delay(80); // 恢复窗口后

        var result = await breaker.ExecuteAsync(_ => Task.FromResult("ok"), CancellationToken.None);
        Assert.Equal("ok", result);
        Assert.Equal(CircuitState.Closed, breaker.State); // 探测成功 → 关闭
    }

    [Fact]
    public async Task Half_open_probe_failure_reopens()
    {
        var breaker = CreateBreaker(failureThreshold: 1, openTimeout: TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            breaker.ExecuteAsync<string>(_ => throw new InvalidOperationException("fail"), CancellationToken.None));

        await Task.Delay(80);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            breaker.ExecuteAsync<string>(_ => throw new InvalidOperationException("fail again"), CancellationToken.None));
        Assert.Equal(CircuitState.Open, breaker.State); // 探测失败 → 回 Open
    }
}

public class RetryPolicyTests
{
    [Fact]
    public async Task Retries_until_success_with_backoff()
    {
        var attempts = 0;
        var policy = new RetryPolicy(maxAttempts: 3, baseDelay: TimeSpan.FromMilliseconds(10));

        var result = await policy.ExecuteAsync(_ =>
        {
            attempts++;
            if (attempts < 3)
            {
                throw new InvalidOperationException("transient");
            }

            return Task.FromResult("ok");
        }, CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task Exhausts_attempts_and_throws()
    {
        var attempts = 0;
        var policy = new RetryPolicy(maxAttempts: 2, baseDelay: TimeSpan.FromMilliseconds(5));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            policy.ExecuteAsync<string>(_ =>
            {
                attempts++;
                throw new InvalidOperationException("always fails");
            }, CancellationToken.None));

        Assert.Equal(2, attempts);
    }
}

public class TimeoutPolicyTests
{
    [Fact]
    public async Task Timeout_aborts_slow_operation()
    {
        var policy = new TimeoutPolicy(TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAsync<TimeoutException>(() =>
            policy.WithTimeoutAsync<string>(async _ =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None);
                return string.Empty;
            }, CancellationToken.None));
    }

    [Fact]
    public async Task Fast_operation_completes()
    {
        var policy = new TimeoutPolicy(TimeSpan.FromSeconds(5));

        var result = await policy.WithTimeoutAsync(_ => Task.FromResult("done"), CancellationToken.None);

        Assert.Equal("done", result);
    }
}
