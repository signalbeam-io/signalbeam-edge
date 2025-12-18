using Microsoft.Extensions.Logging;
using Npgsql;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Resilience;

/// <summary>
/// Defines resilience policies for the TelemetryProcessor infrastructure.
/// Note: Polly policies will be implemented when Polly v8 or later is fully integrated.
/// For now, this provides helper methods for identifying transient errors.
/// </summary>
public static class ResiliencePolicies
{
    /// <summary>
    /// Logs database retry attempts.
    /// </summary>
    public static void LogDatabaseRetry(ILogger logger, Exception exception, int retryCount, TimeSpan delay)
    {
        logger.LogWarning(
            exception,
            "Database operation failed. Retry attempt {RetryCount} after {RetryDelay}ms",
            retryCount,
            delay.TotalMilliseconds);
    }

    /// <summary>
    /// Logs messaging retry attempts.
    /// </summary>
    public static void LogMessagingRetry(ILogger logger, Exception exception, int retryCount, TimeSpan delay)
    {
        logger.LogWarning(
            exception,
            "NATS messaging operation failed. Retry attempt {RetryCount} after {RetryDelay}ms",
            retryCount,
            delay.TotalMilliseconds);
    }

    /// <summary>
    /// Determines if a database exception is transient (retryable).
    /// </summary>
    private static bool IsTransientError(NpgsqlException ex)
    {
        // PostgreSQL transient error codes
        // See: https://www.postgresql.org/docs/current/errcodes-appendix.html
        return ex.SqlState switch
        {
            "08000" => true, // connection_exception
            "08003" => true, // connection_does_not_exist
            "08006" => true, // connection_failure
            "57P03" => true, // cannot_connect_now
            "58000" => true, // system_error
            "58030" => true, // io_error
            "53000" => true, // insufficient_resources
            "53100" => true, // disk_full
            "53200" => true, // out_of_memory
            "53300" => true, // too_many_connections
            "40001" => true, // serialization_failure
            "40P01" => true, // deadlock_detected
            _ => false
        };
    }

    /// <summary>
    /// Determines if a messaging exception is transient (retryable).
    /// </summary>
    private static bool IsTransientMessagingError(Exception ex)
    {
        return ex switch
        {
            TimeoutException => true,
            OperationCanceledException => false, // Don't retry cancellation
            _ => ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                 ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                 ex.Message.Contains("unavailable", StringComparison.OrdinalIgnoreCase)
        };
    }
}
