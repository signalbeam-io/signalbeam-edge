namespace SignalBeam.EdgeAgent.Application.Services;

public interface IMetricsPublisher
{
    Task PublishMetricsAsync(
        Guid deviceId,
        DeviceMetrics metrics,
        int runningContainers,
        CancellationToken cancellationToken = default);
}
