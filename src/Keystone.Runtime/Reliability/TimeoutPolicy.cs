namespace Keystone.Runtime.Reliability;

/// <summary>超时策略（05 §3）：超时中止慢操作并取消其 token（防泄漏）。</summary>
public sealed class TimeoutPolicy
{
    private readonly TimeSpan _timeout;

    public TimeoutPolicy(TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        _timeout = timeout;
    }

    /// <summary>在超时内执行；超时抛 TimeoutException 并取消操作。</summary>
    public async Task<T> WithTimeoutAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var task = action(cts.Token);
        try
        {
            return await task.WaitAsync(_timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            cts.Cancel(); // 取消在途操作，防泄漏
            throw new TimeoutException($"operation timed out after {_timeout}");
        }
    }
}
