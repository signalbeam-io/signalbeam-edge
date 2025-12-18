using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;
using SignalBeam.TelemetryProcessor.Application.Repositories;

namespace SignalBeam.TelemetryProcessor.Application.Commands;

/// <summary>
/// Command to process device metrics.
/// </summary>
public record ProcessMetricsCommand(
    Guid DeviceId,
    DateTimeOffset Timestamp,
    double CpuUsage,
    double MemoryUsage,
    double DiskUsage,
    long UptimeSeconds,
    int RunningContainers,
    string? AdditionalMetrics = null);

/// <summary>
/// Response after processing metrics.
/// </summary>
public record ProcessMetricsResponse(
    Guid MetricsId,
    Guid DeviceId,
    DateTimeOffset Timestamp);

/// <summary>
/// Handler for ProcessMetricsCommand.
/// Stores metrics in TimescaleDB for time-series analysis.
/// </summary>
public class ProcessMetricsHandler
{
    private readonly IDeviceMetricsRepository _metricsRepository;
    private readonly IDeviceRepository _deviceRepository;

    public ProcessMetricsHandler(
        IDeviceMetricsRepository metricsRepository,
        IDeviceRepository deviceRepository)
    {
        _metricsRepository = metricsRepository;
        _deviceRepository = deviceRepository;
    }

    public async Task<Result<ProcessMetricsResponse>> Handle(
        ProcessMetricsCommand command,
        CancellationToken cancellationToken)
    {
        var deviceId = new DeviceId(command.DeviceId);

        // Verify device exists
        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);
        if (device is null)
        {
            var error = Error.NotFound(
                "DEVICE_NOT_FOUND",
                $"Device with ID {command.DeviceId} was not found.");
            return Result.Failure<ProcessMetricsResponse>(error);
        }

        // Create metrics record using domain factory
        DeviceMetrics metrics;
        try
        {
            metrics = DeviceMetrics.Create(
                deviceId,
                command.Timestamp,
                command.CpuUsage,
                command.MemoryUsage,
                command.DiskUsage,
                command.UptimeSeconds,
                command.RunningContainers,
                command.AdditionalMetrics);
        }
        catch (ArgumentException ex)
        {
            var error = Error.Validation(
                "INVALID_METRICS",
                ex.Message);
            return Result.Failure<ProcessMetricsResponse>(error);
        }

        // Store metrics in TimescaleDB
        await _metricsRepository.AddAsync(metrics, cancellationToken);
        await _metricsRepository.SaveChangesAsync(cancellationToken);

        return Result<ProcessMetricsResponse>.Success(new ProcessMetricsResponse(
            metrics.Id,
            deviceId.Value,
            metrics.Timestamp));
    }
}
