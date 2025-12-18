namespace SignalBeam.TelemetryProcessor.Application.MessageHandlers;

/// <summary>
/// Message received from NATS when a device sends a heartbeat.
/// Subject: signalbeam.devices.heartbeat.{deviceId}
/// </summary>
public record DeviceHeartbeatMessage(
    Guid DeviceId,
    DateTimeOffset Timestamp,
    string Status,
    string? IpAddress = null,
    string? AdditionalData = null);
