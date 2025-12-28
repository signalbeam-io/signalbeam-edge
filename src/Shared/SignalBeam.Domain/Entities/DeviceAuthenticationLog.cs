using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Represents an authentication attempt log entry for auditing.
/// </summary>
public class DeviceAuthenticationLog : Entity<Guid>
{
    /// <summary>
    /// The device that attempted to authenticate (null if device couldn't be identified).
    /// </summary>
    public DeviceId? DeviceId { get; private set; }

    /// <summary>
    /// IP address of the authentication request.
    /// </summary>
    public string? IpAddress { get; private set; }

    /// <summary>
    /// User agent of the authentication request.
    /// </summary>
    public string? UserAgent { get; private set; }

    /// <summary>
    /// Whether the authentication was successful.
    /// </summary>
    public bool Success { get; private set; }

    /// <summary>
    /// Reason for failure (if authentication failed).
    /// </summary>
    public string? FailureReason { get; private set; }

    /// <summary>
    /// When the authentication attempt occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; private set; }

    /// <summary>
    /// The API key prefix used (for identification without exposing the key).
    /// </summary>
    public string? ApiKeyPrefix { get; private set; }

    // EF Core constructor
    private DeviceAuthenticationLog() : base(Guid.NewGuid())
    {
    }

    private DeviceAuthenticationLog(
        DeviceId? deviceId,
        string? ipAddress,
        string? userAgent,
        bool success,
        DateTimeOffset timestamp,
        string? failureReason = null,
        string? apiKeyPrefix = null) : base(Guid.NewGuid())
    {
        DeviceId = deviceId;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        Success = success;
        Timestamp = timestamp;
        FailureReason = failureReason;
        ApiKeyPrefix = apiKeyPrefix;
    }

    /// <summary>
    /// Factory method to log a successful authentication.
    /// </summary>
    public static DeviceAuthenticationLog LogSuccess(
        DeviceId deviceId,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset timestamp,
        string? apiKeyPrefix = null)
    {
        return new DeviceAuthenticationLog(
            deviceId,
            ipAddress,
            userAgent,
            success: true,
            timestamp,
            apiKeyPrefix: apiKeyPrefix);
    }

    /// <summary>
    /// Factory method to log a failed authentication.
    /// </summary>
    public static DeviceAuthenticationLog LogFailure(
        DeviceId? deviceId,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset timestamp,
        string failureReason,
        string? apiKeyPrefix = null)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
            throw new ArgumentException("Failure reason is required for failed authentication.", nameof(failureReason));

        return new DeviceAuthenticationLog(
            deviceId,
            ipAddress,
            userAgent,
            success: false,
            timestamp,
            failureReason,
            apiKeyPrefix);
    }
}
