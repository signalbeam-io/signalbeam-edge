using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace SignalBeam.DeviceManager.Infrastructure.Resilience;

/// <summary>
/// Centralized Polly resilience pipeline configurations for the DeviceManager service.
/// Uses Polly v8 ResiliencePipeline API.
/// </summary>
public static class ResiliencePolicies
{
    /// <summary>
    /// Retry pipeline for database operations.
    /// Retries 3 times with exponential backoff.
    /// </summary>
    public static ResiliencePipeline DatabaseRetryPipeline => new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromSeconds(1),
            ShouldHandle = new PredicateBuilder().Handle<Exception>(ex => IsTransientException(ex)),
            OnRetry = args =>
            {
                Console.WriteLine(
                    $"Database retry {args.AttemptNumber} after {args.RetryDelay.TotalSeconds}s due to: {args.Outcome.Exception?.Message}");
                return ValueTask.CompletedTask;
            }
        })
        .Build();

    /// <summary>
    /// Circuit breaker pipeline for external services (NATS, Azure Blob Storage).
    /// Opens circuit after 5 consecutive failures, stays open for 30 seconds.
    /// </summary>
    public static ResiliencePipeline ExternalServiceCircuitBreakerPipeline => new ResiliencePipelineBuilder()
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(10),
            MinimumThroughput = 5,
            BreakDuration = TimeSpan.FromSeconds(30),
            ShouldHandle = new PredicateBuilder().Handle<Exception>(ex => !IsFatalException(ex)),
            OnOpened = args =>
            {
                Console.WriteLine(
                    $"Circuit breaker opened for {args.BreakDuration.TotalSeconds}s");
                return ValueTask.CompletedTask;
            },
            OnClosed = args =>
            {
                Console.WriteLine("Circuit breaker reset");
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = args =>
            {
                Console.WriteLine("Circuit breaker half-open, testing service");
                return ValueTask.CompletedTask;
            }
        })
        .Build();

    /// <summary>
    /// Retry pipeline for HTTP client operations.
    /// Retries 2 times for transient HTTP errors.
    /// </summary>
    public static ResiliencePipeline HttpRetryPipeline => new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 2,
            BackoffType = DelayBackoffType.Linear,
            Delay = TimeSpan.FromMilliseconds(100),
            ShouldHandle = new PredicateBuilder()
                .Handle<HttpRequestException>()
                .Handle<TaskCanceledException>(),
            OnRetry = args =>
            {
                Console.WriteLine(
                    $"HTTP retry {args.AttemptNumber} after {args.RetryDelay.TotalMilliseconds}ms due to: {args.Outcome.Exception?.Message}");
                return ValueTask.CompletedTask;
            }
        })
        .Build();

    /// <summary>
    /// Retry pipeline for NATS message publishing.
    /// Retries 3 times with exponential backoff.
    /// </summary>
    public static ResiliencePipeline NatsRetryPipeline => new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromMilliseconds(500),
            ShouldHandle = new PredicateBuilder().Handle<Exception>(ex => IsTransientException(ex)),
            OnRetry = args =>
            {
                Console.WriteLine(
                    $"NATS retry {args.AttemptNumber} after {args.RetryDelay.TotalMilliseconds}ms due to: {args.Outcome.Exception?.Message}");
                return ValueTask.CompletedTask;
            }
        })
        .Build();

    /// <summary>
    /// Combined pipeline: Retry + Circuit Breaker for external services.
    /// </summary>
    public static ResiliencePipeline ExternalServicePipeline => new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 2,
            BackoffType = DelayBackoffType.Linear,
            Delay = TimeSpan.FromMilliseconds(100)
        })
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(10),
            MinimumThroughput = 5,
            BreakDuration = TimeSpan.FromSeconds(30)
        })
        .Build();

    /// <summary>
    /// Determines if an exception is transient and should be retried.
    /// </summary>
    private static bool IsTransientException(Exception ex)
    {
        // Database transient exceptions
        if (ex is Npgsql.NpgsqlException npgsqlEx)
        {
            return npgsqlEx.IsTransient;
        }

        // Network transient exceptions
        if (ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is TimeoutException ||
            ex is System.Net.Sockets.SocketException)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if an exception is fatal and should not be retried.
    /// </summary>
    private static bool IsFatalException(Exception ex)
    {
        return ex is ArgumentException ||
               ex is ArgumentNullException ||
               ex is InvalidOperationException ||
               ex is UnauthorizedAccessException;
    }
}
