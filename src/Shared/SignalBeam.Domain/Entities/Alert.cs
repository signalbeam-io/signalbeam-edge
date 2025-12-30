using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Represents an alert raised by the system when a monitored condition is met.
/// Alerts have a lifecycle: Active → Acknowledged → Resolved.
/// </summary>
public class Alert : Entity<Guid>
{
    private Alert()
    {
        // Required for EF Core
    }

    /// <summary>
    /// Gets the tenant this alert belongs to.
    /// </summary>
    public TenantId TenantId { get; private set; } = default!;

    /// <summary>
    /// Gets the alert severity level.
    /// </summary>
    public AlertSeverity Severity { get; private set; }

    /// <summary>
    /// Gets the type of alert.
    /// </summary>
    public AlertType Type { get; private set; }

    /// <summary>
    /// Gets the alert title (short summary).
    /// </summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the alert description (detailed message).
    /// </summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the device ID if this alert is related to a specific device.
    /// </summary>
    public DeviceId? DeviceId { get; private set; }

    /// <summary>
    /// Gets the rollout ID if this alert is related to a specific rollout.
    /// </summary>
    public Guid? RolloutId { get; private set; }

    /// <summary>
    /// Gets the current status of the alert.
    /// </summary>
    public AlertStatus Status { get; private set; }

    /// <summary>
    /// Gets the timestamp when the alert was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Gets the timestamp when the alert was acknowledged.
    /// </summary>
    public DateTimeOffset? AcknowledgedAt { get; private set; }

    /// <summary>
    /// Gets the identifier of who acknowledged the alert.
    /// </summary>
    public string? AcknowledgedBy { get; private set; }

    /// <summary>
    /// Gets the timestamp when the alert was resolved.
    /// </summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>
    /// Creates a new alert.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="severity">Alert severity.</param>
    /// <param name="type">Alert type.</param>
    /// <param name="title">Alert title.</param>
    /// <param name="description">Alert description.</param>
    /// <param name="deviceId">Optional device identifier.</param>
    /// <param name="rolloutId">Optional rollout identifier.</param>
    /// <returns>A new Alert instance.</returns>
    public static Alert Create(
        TenantId tenantId,
        AlertSeverity severity,
        AlertType type,
        string title,
        string description,
        DeviceId? deviceId = null,
        Guid? rolloutId = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Alert title cannot be empty.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Alert description cannot be empty.", nameof(description));
        }

        return new Alert
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Severity = severity,
            Type = type,
            Title = title,
            Description = description,
            DeviceId = deviceId,
            RolloutId = rolloutId,
            Status = AlertStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Acknowledges the alert, indicating an operator is aware and working on it.
    /// </summary>
    /// <param name="acknowledgedBy">Identifier of who acknowledged the alert.</param>
    /// <param name="acknowledgedAt">When the alert was acknowledged.</param>
    /// <exception cref="InvalidOperationException">Thrown when alert is already resolved.</exception>
    public void Acknowledge(string acknowledgedBy, DateTimeOffset acknowledgedAt)
    {
        if (Status == AlertStatus.Resolved)
        {
            throw new InvalidOperationException("Cannot acknowledge an alert that is already resolved.");
        }

        if (string.IsNullOrWhiteSpace(acknowledgedBy))
        {
            throw new ArgumentException("Acknowledged by cannot be empty.", nameof(acknowledgedBy));
        }

        Status = AlertStatus.Acknowledged;
        AcknowledgedBy = acknowledgedBy;
        AcknowledgedAt = acknowledgedAt;
    }

    /// <summary>
    /// Resolves the alert, indicating the underlying condition has been fixed.
    /// </summary>
    /// <param name="resolvedAt">When the alert was resolved.</param>
    /// <exception cref="InvalidOperationException">Thrown when alert is already resolved.</exception>
    public void Resolve(DateTimeOffset resolvedAt)
    {
        if (Status == AlertStatus.Resolved)
        {
            throw new InvalidOperationException("Alert is already resolved.");
        }

        Status = AlertStatus.Resolved;
        ResolvedAt = resolvedAt;
    }

    /// <summary>
    /// Determines if the alert is still active (not acknowledged or resolved).
    /// </summary>
    public bool IsActive => Status == AlertStatus.Active;

    /// <summary>
    /// Determines if the alert has been resolved.
    /// </summary>
    public bool IsResolved => Status == AlertStatus.Resolved;

    /// <summary>
    /// Gets the age of the alert (time since creation).
    /// </summary>
    public TimeSpan GetAge(DateTimeOffset currentTime)
    {
        return currentTime - CreatedAt;
    }

    /// <summary>
    /// Gets the time to acknowledgment (TTR - Time To Response).
    /// </summary>
    public TimeSpan? GetTimeToAcknowledge()
    {
        return AcknowledgedAt.HasValue ? AcknowledgedAt.Value - CreatedAt : null;
    }

    /// <summary>
    /// Gets the time to resolution (TTM - Time To Mitigate).
    /// </summary>
    public TimeSpan? GetTimeToResolve()
    {
        return ResolvedAt.HasValue ? ResolvedAt.Value - CreatedAt : null;
    }
}
