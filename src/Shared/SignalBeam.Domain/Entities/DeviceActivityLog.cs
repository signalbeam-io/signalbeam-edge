using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Represents an activity log entry for a device.
/// Tracks all significant events and actions related to a device.
/// </summary>
public class DeviceActivityLog : Entity<Guid>
{
    /// <summary>
    /// Device this activity is related to.
    /// </summary>
    public DeviceId DeviceId { get; private set; }

    /// <summary>
    /// Timestamp of the activity (UTC).
    /// </summary>
    public DateTimeOffset Timestamp { get; private set; }

    /// <summary>
    /// Type of activity (e.g., "DeviceRegistered", "BundleAssigned", "StatusChanged").
    /// </summary>
    public string ActivityType { get; private set; } = string.Empty;

    /// <summary>
    /// Human-readable description of the activity.
    /// </summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Severity level of the activity (Info, Warning, Error).
    /// </summary>
    public string Severity { get; private set; } = "Info";

    /// <summary>
    /// Additional metadata as JSON (optional).
    /// </summary>
    public string? Metadata { get; private set; }

    // EF Core constructor
    private DeviceActivityLog() : base()
    {
    }

    private DeviceActivityLog(
        Guid id,
        DeviceId deviceId,
        DateTimeOffset timestamp,
        string activityType,
        string description,
        string severity,
        string? metadata = null) : base(id)
    {
        DeviceId = deviceId;
        Timestamp = timestamp;
        ActivityType = activityType;
        Description = description;
        Severity = severity;
        Metadata = metadata;
    }

    /// <summary>
    /// Factory method to create an activity log entry.
    /// </summary>
    public static DeviceActivityLog Create(
        DeviceId deviceId,
        DateTimeOffset timestamp,
        string activityType,
        string description,
        string severity = "Info",
        string? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(activityType))
            throw new ArgumentException("Activity type cannot be empty.", nameof(activityType));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty.", nameof(description));

        return new DeviceActivityLog(
            Guid.NewGuid(),
            deviceId,
            timestamp,
            activityType,
            description,
            severity,
            metadata);
    }
}
