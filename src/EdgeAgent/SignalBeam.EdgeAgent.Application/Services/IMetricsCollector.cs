namespace SignalBeam.EdgeAgent.Application.Services;

public interface IMetricsCollector
{
    Task<DeviceMetrics> CollectMetricsAsync(CancellationToken cancellationToken = default);
}
