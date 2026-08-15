using Keystone.Core.Errors;

namespace Keystone.Runtime.Reliability;

/// <summary>熔断器（05 §3）：Open 快速失败 + 恢复窗口 + 半开探测。</summary>
public sealed class CircuitBreaker
{
    private readonly CircuitBreakerOptions _options;
    private readonly Lock _lock = new();
    private CircuitState _state = CircuitState.Closed;
    private int _consecutiveFailures;
    private DateTimeOffset _openUntil;

    public CircuitBreaker(CircuitBreakerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public CircuitState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    /// <summary>执行受保护操作；Open 时快速失败（熔断拒绝）。</summary>
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        switch (Probe())
        {
            case CircuitState.Open:
                throw new KeystoneException(
                    ErrorCode.ReliabilityCircuitOpen,
                    $"circuit is open for {_options.OpenTimeout}");

            case CircuitState.HalfOpen:
                break;
        }

        try
        {
            var result = await action(cancellationToken).ConfigureAwait(false);
            OnSuccess();
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            OnFailure();
            throw;
        }
    }

    private CircuitState Probe()
    {
        lock (_lock)
        {
            if (_state == CircuitState.Open && DateTimeOffset.UtcNow >= _openUntil)
            {
                _state = CircuitState.HalfOpen; // 恢复窗口后允许探测
            }

            return _state;
        }
    }

    private void OnSuccess()
    {
        lock (_lock)
        {
            _consecutiveFailures = 0;
            _state = CircuitState.Closed;
        }
    }

    private void OnFailure()
    {
        lock (_lock)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= _options.FailureThreshold)
            {
                _state = CircuitState.Open;
                _openUntil = DateTimeOffset.UtcNow + _options.OpenTimeout;
            }
        }
    }
}
