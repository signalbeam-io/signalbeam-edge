using Polly;
using Polly.Retry;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace SignalBeam.Shared.Infrastructure.Resilience;

/// <summary>
/// Provides pre-configured Polly resilience policies.
/// </summary>
public static class PollyPolicies
{
    /// <summary>
    /// Creates a retry policy with exponential backoff.
    /// </summary>
    /// <param name="retryCount">Number of retry attempts.</param>
    /// <param name="baseDelay">Base delay between retries.</param>
    public static AsyncRetryPolicy CreateRetryPolicy(
        int retryCount = 3,
        TimeSpan? baseDelay = null)
    {
        var delay = baseDelay ?? TimeSpan.FromSeconds(1);

        return Policy
            .Handle<Exception>(ex => IsTransient(ex))
            .WaitAndRetryAsync(
                retryCount,
                retryAttempt => delay * Math.Pow(2, retryAttempt - 1),
                onRetry: (exception, timeSpan, retry, context) =>
                {
                    // TODO: Add logging here
                });
    }

    /// <summary>
    /// Creates a typed retry policy with exponential backoff.
    /// </summary>
    public static AsyncRetryPolicy<TResult> CreateRetryPolicy<TResult>(
        int retryCount = 3,
        TimeSpan? baseDelay = null,
        Func<TResult, bool>? shouldRetry = null)
    {
        var delay = baseDelay ?? TimeSpan.FromSeconds(1);

        var policy = Policy
            .HandleResult<TResult>(result => shouldRetry?.Invoke(result) ?? false)
            .Or<Exception>(ex => IsTransient(ex));

        return policy.WaitAndRetryAsync(
            retryCount,
            retryAttempt => delay * Math.Pow(2, retryAttempt - 1));
    }

    /// <summary>
    /// Creates a circuit breaker policy.
    /// </summary>
    /// <param name="failureThreshold">Number of failures before breaking the circuit.</param>
    /// <param name="durationOfBreak">Duration to keep the circuit open.</param>
    public static AsyncCircuitBreakerPolicy CreateCircuitBreakerPolicy(
        int failureThreshold = 5,
        TimeSpan? durationOfBreak = null)
    {
        var breakDuration = durationOfBreak ?? TimeSpan.FromSeconds(30);

        return Policy
            .Handle<Exception>(ex => IsTransient(ex))
            .CircuitBreakerAsync(
                failureThreshold,
                breakDuration,
                onBreak: (exception, duration) =>
                {
                    // TODO: Add logging here
                },
                onReset: () =>
                {
                    // TODO: Add logging here
                },
                onHalfOpen: () =>
                {
                    // TODO: Add logging here
                });
    }

    /// <summary>
    /// Creates a timeout policy.
    /// </summary>
    /// <param name="timeout">The timeout duration.</param>
    public static AsyncTimeoutPolicy CreateTimeoutPolicy(TimeSpan? timeout = null)
    {
        var timeoutDuration = timeout ?? TimeSpan.FromSeconds(30);

        return Policy.TimeoutAsync(timeoutDuration, TimeoutStrategy.Pessimistic);
    }

    /// <summary>
    /// Creates a combined policy with retry, circuit breaker, and timeout.
    /// </summary>
    public static IAsyncPolicy CreateCombinedPolicy(
        int retryCount = 3,
        int circuitBreakerThreshold = 5,
        TimeSpan? timeout = null)
    {
        var retryPolicy = CreateRetryPolicy(retryCount);
        var circuitBreaker = CreateCircuitBreakerPolicy(circuitBreakerThreshold);
        var timeoutPolicy = CreateTimeoutPolicy(timeout);

        // Wrap policies: timeout -> retry -> circuit breaker
        return Policy.WrapAsync(timeoutPolicy, retryPolicy, circuitBreaker);
    }

    /// <summary>
    /// Determines if an exception is transient and should be retried.
    /// </summary>
    private static bool IsTransient(Exception exception)
    {
        return exception is TimeoutException
            or HttpRequestException
            or TaskCanceledException
            or OperationCanceledException;
    }
}
