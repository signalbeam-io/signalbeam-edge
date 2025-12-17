using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;
using Microsoft.AspNetCore.Mvc;

namespace SignalBeam.DeviceManager.Application.Queries;

public record GetDeviceMetricsQuery(
    [FromRoute] Guid DeviceId,
    [FromQuery] DateTimeOffset? StartDate = null,
    [FromQuery] DateTimeOffset? EndDate = null,
    [FromQuery] int PageNumber = 1,
    [FromQuery] int PageSize = 50);

public record DeviceMetricsEntry(
    Guid Id,
    Guid DeviceId,
    DateTimeOffset Timestamp,
    double CpuUsage,
    double MemoryUsage,
    double DiskUsage,
    long UptimeSeconds,
    int RunningContainers,
    string? AdditionalMetrics);

public record GetDeviceMetricsResponse(
    IReadOnlyCollection<DeviceMetricsEntry> Metrics,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages);

/// <summary>
/// Handler for retrieving device metrics history.
/// </summary>
public class GetDeviceMetricsHandler
{
    private readonly IDeviceMetricsQueryRepository _queryRepository;

    public GetDeviceMetricsHandler(IDeviceMetricsQueryRepository queryRepository)
    {
        _queryRepository = queryRepository;
    }

    public async Task<Result<GetDeviceMetricsResponse>> Handle(
        GetDeviceMetricsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.PageNumber < 1 || query.PageSize < 1 || query.PageSize > 100)
        {
            var error = Error.Validation(
                "INVALID_PAGINATION",
                "Page number must be >= 1 and page size must be between 1 and 100.");
            return Result.Failure<GetDeviceMetricsResponse>(error);
        }

        if (query.StartDate.HasValue && query.EndDate.HasValue && query.StartDate > query.EndDate)
        {
            var error = Error.Validation(
                "INVALID_DATE_RANGE",
                "Start date must be before end date.");
            return Result.Failure<GetDeviceMetricsResponse>(error);
        }

        var deviceId = new DeviceId(query.DeviceId);

        var (metrics, totalCount) = await _queryRepository.GetMetricsHistoryAsync(
            deviceId,
            query.StartDate,
            query.EndDate,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        var metricsEntries = metrics.Select(m => new DeviceMetricsEntry(
            Id: m.Id,
            DeviceId: m.DeviceId.Value,
            Timestamp: m.Timestamp,
            CpuUsage: m.CpuUsage,
            MemoryUsage: m.MemoryUsage,
            DiskUsage: m.DiskUsage,
            UptimeSeconds: m.UptimeSeconds,
            RunningContainers: m.RunningContainers,
            AdditionalMetrics: m.AdditionalMetrics
        )).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

        return Result<GetDeviceMetricsResponse>.Success(new GetDeviceMetricsResponse(
            Metrics: metricsEntries,
            TotalCount: totalCount,
            PageNumber: query.PageNumber,
            PageSize: query.PageSize,
            TotalPages: totalPages));
    }
}
