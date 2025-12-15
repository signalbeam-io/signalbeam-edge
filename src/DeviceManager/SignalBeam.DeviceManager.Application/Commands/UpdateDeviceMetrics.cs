using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

public record UpdateDeviceMetricsCommand(
    Guid DeviceId,
    DateTimeOffset Timestamp,
    double CpuUsage,
    double MemoryUsage,
    double DiskUsage,
    long UptimeSeconds,
    int RunningContainers,
    string? AdditionalMetrics = null);

public record UpdateDeviceMetricsResponse(
    Guid MetricsId,
    Guid DeviceId,
    DateTimeOffset Timestamp);

/// <summary>
/// Handler for updating device metrics.
/// Records metrics to TimescaleDB for time-series analysis.
/// </summary>
public class UpdateDeviceMetricsHandler
{
    private readonly IDeviceMetricsRepository _metricsRepository;

    public UpdateDeviceMetricsHandler(IDeviceMetricsRepository metricsRepository)
    {
        _metricsRepository = metricsRepository;
    }

    public async Task<Result<UpdateDeviceMetricsResponse>> Handle(
        UpdateDeviceMetricsCommand command,
        CancellationToken cancellationToken)
    {
        var deviceId = new DeviceId(command.DeviceId);

        var metrics = DeviceMetrics.Create(
            deviceId,
            command.Timestamp,
            command.CpuUsage,
            command.MemoryUsage,
            command.DiskUsage,
            command.UptimeSeconds,
            command.RunningContainers,
            command.AdditionalMetrics);

        await _metricsRepository.AddAsync(metrics, cancellationToken);
        await _metricsRepository.SaveChangesAsync(cancellationToken);

        return Result<UpdateDeviceMetricsResponse>.Success(new UpdateDeviceMetricsResponse(
            MetricsId: metrics.Id,
            DeviceId: metrics.DeviceId.Value,
            Timestamp: metrics.Timestamp));
    }
}
