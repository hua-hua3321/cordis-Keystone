namespace Keystone.Runtime.Reliability;

/// <summary>重试策略（05 §4）：指数退避（2^n × baseDelay）。</summary>
public sealed class RetryPolicy
{
    private readonly int _maxAttempts;
    private readonly TimeSpan _baseDelay;

    public RetryPolicy(int maxAttempts, TimeSpan baseDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

        _maxAttempts = maxAttempts;
        _baseDelay = baseDelay;
    }

    /// <summary>执行操作并重试（指数退避；达到最大次数后重抛）。</summary>
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await action(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (attempt < _maxAttempts - 1 && !cancellationToken.IsCancellationRequested)
            {
                var delay = TimeSpan.FromMilliseconds(_baseDelay.TotalMilliseconds * (1 << attempt));
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
