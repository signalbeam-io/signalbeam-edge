using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;
using SignalBeam.TelemetryProcessor.Application.Repositories;

namespace SignalBeam.TelemetryProcessor.Application.Queries;

/// <summary>
/// Query to get aggregated device metrics from TimescaleDB continuous aggregates.
/// </summary>
public record GetAggregatedMetricsQuery(
    Guid DeviceId,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    AggregationInterval Interval = AggregationInterval.Hourly);

/// <summary>
/// Aggregation interval for metrics.
/// </summary>
public enum AggregationInterval
{
    Hourly,
    Daily
}

/// <summary>
/// Response containing aggregated metrics.
/// </summary>
public record GetAggregatedMetricsResponse(
    Guid DeviceId,
    AggregationInterval Interval,
    IReadOnlyCollection<MetricsAggregateDto> Aggregates);

/// <summary>
/// Aggregated metrics data transfer object.
/// </summary>
public record MetricsAggregateDto(
    DateTimeOffset Bucket,
    double AvgCpuUsage,
    double MaxCpuUsage,
    double MinCpuUsage,
    double AvgMemoryUsage,
    double MaxMemoryUsage,
    double MinMemoryUsage,
    double AvgDiskUsage,
    double MaxDiskUsage,
    double MinDiskUsage,
    int DataPoints);

/// <summary>
/// Handler for GetAggregatedMetricsQuery.
/// Queries TimescaleDB continuous aggregates for fast dashboard rendering.
/// </summary>
public class GetAggregatedMetricsHandler
{
    private readonly IMetricsAggregateRepository _aggregateRepository;

    public GetAggregatedMetricsHandler(IMetricsAggregateRepository aggregateRepository)
    {
        _aggregateRepository = aggregateRepository;
    }

    public async Task<Result<GetAggregatedMetricsResponse>> Handle(
        GetAggregatedMetricsQuery query,
        CancellationToken cancellationToken)
    {
        var deviceId = new DeviceId(query.DeviceId);

        // Validate time range
        if (query.StartTime >= query.EndTime)
        {
            var error = Error.Validation(
                "INVALID_TIME_RANGE",
                "Start time must be before end time.");
            return Result.Failure<GetAggregatedMetricsResponse>(error);
        }

        // Query based on aggregation interval
        IReadOnlyCollection<MetricsAggregateDto> aggregateDtos;

        if (query.Interval == AggregationInterval.Hourly)
        {
            var aggregates = await _aggregateRepository.GetHourlyAggregatesAsync(
                deviceId,
                query.StartTime,
                query.EndTime,
                cancellationToken);

            aggregateDtos = aggregates.Select(a => new MetricsAggregateDto(
                a.Bucket,
                a.AvgCpuUsage,
                a.MaxCpuUsage,
                a.MinCpuUsage,
                a.AvgMemoryUsage,
                a.MaxMemoryUsage,
                a.MinMemoryUsage,
                a.AvgDiskUsage,
                a.MaxDiskUsage,
                a.MinDiskUsage,
                a.DataPoints)).ToList();
        }
        else // Daily
        {
            var aggregates = await _aggregateRepository.GetDailyAggregatesAsync(
                deviceId,
                query.StartTime,
                query.EndTime,
                cancellationToken);

            aggregateDtos = aggregates.Select(a => new MetricsAggregateDto(
                a.Bucket,
                a.AvgCpuUsage,
                a.MaxCpuUsage,
                a.MinCpuUsage,
                a.AvgMemoryUsage,
                a.MaxMemoryUsage,
                a.MinMemoryUsage,
                a.AvgDiskUsage,
                a.MaxDiskUsage,
                a.MinDiskUsage,
                a.DataPoints)).ToList();
        }

        return Result<GetAggregatedMetricsResponse>.Success(new GetAggregatedMetricsResponse(
            deviceId.Value,
            query.Interval,
            aggregateDtos));
    }
}
