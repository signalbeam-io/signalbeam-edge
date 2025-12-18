using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;
using SignalBeam.TelemetryProcessor.Application.Repositories;

namespace SignalBeam.TelemetryProcessor.Application.Queries;

/// <summary>
/// Query to get device metrics history.
/// </summary>
public record GetDeviceMetricsQuery(
    Guid DeviceId,
    DateTimeOffset? StartTime = null,
    DateTimeOffset? EndTime = null,
    int PageNumber = 1,
    int PageSize = 100);

/// <summary>
/// Response containing device metrics.
/// </summary>
public record GetDeviceMetricsResponse(
    IReadOnlyCollection<DeviceMetricsDto> Metrics,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages);

/// <summary>
/// Device metrics data transfer object.
/// </summary>
public record DeviceMetricsDto(
    Guid Id,
    Guid DeviceId,
    DateTimeOffset Timestamp,
    double CpuUsage,
    double MemoryUsage,
    double DiskUsage,
    long UptimeSeconds,
    int RunningContainers,
    string? AdditionalMetrics);

/// <summary>
/// Handler for GetDeviceMetricsQuery.
/// </summary>
public class GetDeviceMetricsHandler
{
    private readonly IDeviceMetricsRepository _metricsRepository;

    public GetDeviceMetricsHandler(IDeviceMetricsRepository metricsRepository)
    {
        _metricsRepository = metricsRepository;
    }

    public async Task<Result<GetDeviceMetricsResponse>> Handle(
        GetDeviceMetricsQuery query,
        CancellationToken cancellationToken)
    {
        var deviceId = new DeviceId(query.DeviceId);

        // Default time range if not specified: last 24 hours
        var startTime = query.StartTime ?? DateTimeOffset.UtcNow.AddHours(-24);
        var endTime = query.EndTime ?? DateTimeOffset.UtcNow;

        // Validate time range
        if (startTime >= endTime)
        {
            var error = Error.Validation(
                "INVALID_TIME_RANGE",
                "Start time must be before end time.");
            return Result.Failure<GetDeviceMetricsResponse>(error);
        }

        // Get paginated metrics
        var (metrics, totalCount) = await _metricsRepository.GetMetricsHistoryAsync(
            deviceId,
            startTime,
            endTime,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        // Map to DTOs
        var metricsDtos = metrics.Select(m => new DeviceMetricsDto(
            m.Id,
            m.DeviceId.Value,
            m.Timestamp,
            m.CpuUsage,
            m.MemoryUsage,
            m.DiskUsage,
            m.UptimeSeconds,
            m.RunningContainers,
            m.AdditionalMetrics)).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

        return Result<GetDeviceMetricsResponse>.Success(new GetDeviceMetricsResponse(
            metricsDtos,
            totalCount,
            query.PageNumber,
            query.PageSize,
            totalPages));
    }
}
