using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Represents an audit log event for a device.
/// Tracks important events in the device lifecycle.
/// </summary>
public class DeviceEvent : Entity<Guid>
{
    /// <summary>
    /// Device this event belongs to.
    /// </summary>
    public DeviceId DeviceId { get; private set; }

    /// <summary>
    /// When the event occurred (UTC).
    /// </summary>
    public DateTimeOffset Timestamp { get; private set; }

    /// <summary>
    /// Event type (e.g., "DeviceRegistered", "BundleAssigned", "UpdateCompleted").
    /// </summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>
    /// Event severity level.
    /// </summary>
    public EventSeverity Severity { get; private set; }

    /// <summary>
    /// Human-readable event message.
    /// </summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>
    /// Additional event data as JSON.
    /// </summary>
    public string? DataJson { get; private set; }

    /// <summary>
    /// User or system that triggered this event.
    /// </summary>
    public string? TriggeredBy { get; private set; }

    // EF Core constructor
    private DeviceEvent() : base()
    {
    }

    private DeviceEvent(
        Guid id,
        DeviceId deviceId,
        DateTimeOffset timestamp,
        string eventType,
        EventSeverity severity,
        string message) : base(id)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("Event type cannot be empty.", nameof(eventType));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be empty.", nameof(message));

        DeviceId = deviceId;
        Timestamp = timestamp;
        EventType = eventType;
        Severity = severity;
        Message = message;
    }

    /// <summary>
    /// Factory method to create a device event.
    /// </summary>
    public static DeviceEvent Create(
        DeviceId deviceId,
        DateTimeOffset timestamp,
        string eventType,
        EventSeverity severity,
        string message,
        string? dataJson = null,
        string? triggeredBy = null)
    {
        return new DeviceEvent(Guid.NewGuid(), deviceId, timestamp, eventType, severity, message)
        {
            DataJson = dataJson,
            TriggeredBy = triggeredBy
        };
    }
}

/// <summary>
/// Event severity levels.
/// </summary>
public enum EventSeverity
{
    Information = 0,
    Warning = 1,
    Error = 2,
    Critical = 3
}
