namespace SignalBeam.EdgeAgent.Application.Services;

public interface IHeartbeatPublisher
{
    Task PublishHeartbeatAsync(
        Guid deviceId,
        string status,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);
}
