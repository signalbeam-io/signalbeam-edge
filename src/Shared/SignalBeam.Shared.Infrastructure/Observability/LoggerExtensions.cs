using Microsoft.Extensions.Logging;

namespace SignalBeam.Shared.Infrastructure.Observability;

/// <summary>
/// Extension methods for structured logging.
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    /// Logs device registration.
    /// </summary>
    public static void LogDeviceRegistered(
        this ILogger logger,
        string deviceId,
        string tenantId,
        string deviceName)
    {
        logger.LogInformation(
            "Device {DeviceId} registered for tenant {TenantId} with name {DeviceName}",
            deviceId,
            tenantId,
            deviceName);
    }

    /// <summary>
    /// Logs device heartbeat.
    /// </summary>
    public static void LogDeviceHeartbeat(
        this ILogger logger,
        string deviceId,
        double? cpuUsage,
        double? memoryUsage)
    {
        logger.LogDebug(
            "Heartbeat received from device {DeviceId}: CPU={CpuUsage}%, Memory={MemoryUsage}%",
            deviceId,
            cpuUsage,
            memoryUsage);
    }

    /// <summary>
    /// Logs bundle assignment.
    /// </summary>
    public static void LogBundleAssigned(
        this ILogger logger,
        string deviceId,
        string bundleId,
        string version)
    {
        logger.LogInformation(
            "Bundle {BundleId} version {Version} assigned to device {DeviceId}",
            bundleId,
            version,
            deviceId);
    }

    /// <summary>
    /// Logs message publishing.
    /// </summary>
    public static void LogMessagePublished(
        this ILogger logger,
        string subject,
        string messageId)
    {
        logger.LogDebug(
            "Published message {MessageId} to subject {Subject}",
            messageId,
            subject);
    }

    /// <summary>
    /// Logs message processing error.
    /// </summary>
    public static void LogMessageProcessingError(
        this ILogger logger,
        string subject,
        string messageId,
        Exception exception)
    {
        logger.LogError(
            exception,
            "Error processing message {MessageId} from subject {Subject}",
            messageId,
            subject);
    }

    /// <summary>
    /// Logs API key validation failure.
    /// </summary>
    public static void LogApiKeyValidationFailed(
        this ILogger logger,
        string reason)
    {
        logger.LogWarning(
            "API key validation failed: {Reason}",
            reason);
    }

    /// <summary>
    /// Logs circuit breaker state change.
    /// </summary>
    public static void LogCircuitBreakerStateChanged(
        this ILogger logger,
        string circuitName,
        string newState)
    {
        logger.LogWarning(
            "Circuit breaker {CircuitName} state changed to {NewState}",
            circuitName,
            newState);
    }
}
