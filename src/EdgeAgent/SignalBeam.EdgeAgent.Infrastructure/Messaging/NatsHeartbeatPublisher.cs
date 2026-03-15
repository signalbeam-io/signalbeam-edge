using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.Shared.Infrastructure.Messaging;

namespace SignalBeam.EdgeAgent.Infrastructure.Messaging;

public sealed class NatsHeartbeatPublisher : IHeartbeatPublisher
{
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<NatsHeartbeatPublisher> _logger;

    public NatsHeartbeatPublisher(
        IMessagePublisher publisher,
        ILogger<NatsHeartbeatPublisher> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task PublishHeartbeatAsync(
        Guid deviceId,
        string status,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        var subject = $"signalbeam.devices.heartbeat.{deviceId}";

        var message = new DeviceHeartbeatMessage(
            DeviceId: deviceId,
            Timestamp: DateTimeOffset.UtcNow,
            Status: status,
            IpAddress: ipAddress);

        _logger.LogDebug(
            "Publishing heartbeat for device {DeviceId} to {Subject}",
            deviceId, subject);

        await _publisher.PublishAsync(subject, message, cancellationToken);
    }
}

/// <summary>
/// Message schema matching TelemetryProcessor's DeviceHeartbeatMessage.
/// Published to Core NATS (ephemeral).
/// </summary>
internal record DeviceHeartbeatMessage(
    Guid DeviceId,
    DateTimeOffset Timestamp,
    string Status,
    string? IpAddress = null,
    string? AdditionalData = null);
