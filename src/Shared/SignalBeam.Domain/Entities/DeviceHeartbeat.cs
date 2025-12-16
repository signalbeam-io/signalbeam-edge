using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Represents a heartbeat from a device.
/// Stored as TimescaleDB hypertable for efficient time-series queries.
/// </summary>
public class DeviceHeartbeat : Entity<Guid>
{
    /// <summary>
    /// Device that sent this heartbeat.
    /// </summary>
    public DeviceId DeviceId { get; private set; }

    /// <summary>
    /// Timestamp when heartbeat was received (UTC).
    /// This is the time dimension for TimescaleDB hypertable.
    /// </summary>
    public DateTimeOffset Timestamp { get; private set; }

    /// <summary>
    /// Device status at the time of heartbeat.
    /// </summary>
    public string Status { get; private set; } = string.Empty;

    /// <summary>
    /// IP address of the device (optional).
    /// </summary>
    public string? IpAddress { get; private set; }

    /// <summary>
    /// Additional heartbeat data as JSON (optional).
    /// Can include network info, current workload, etc.
    /// </summary>
    public string? AdditionalData { get; private set; }

    // EF Core constructor
    private DeviceHeartbeat() : base()
    {
    }

    private DeviceHeartbeat(
        Guid id,
        DeviceId deviceId,
        DateTimeOffset timestamp,
        string status,
        string? ipAddress = null,
        string? additionalData = null) : base(id)
    {
        DeviceId = deviceId;
        Timestamp = timestamp;
        Status = status;
        IpAddress = ipAddress;
        AdditionalData = additionalData;
    }

    /// <summary>
    /// Factory method to create a device heartbeat.
    /// </summary>
    public static DeviceHeartbeat Create(
        DeviceId deviceId,
        DateTimeOffset timestamp,
        string status,
        string? ipAddress = null,
        string? additionalData = null)
    {
        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Status cannot be empty.", nameof(status));

        return new DeviceHeartbeat(
            Guid.NewGuid(),
            deviceId,
            timestamp,
            status,
            ipAddress,
            additionalData);
    }
}
