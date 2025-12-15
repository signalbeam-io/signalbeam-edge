using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;
using Microsoft.AspNetCore.Mvc;

namespace SignalBeam.DeviceManager.Application.Queries;

public record GetDeviceActivityLogQuery(
    [FromRoute] Guid DeviceId,
    [FromQuery] int PageNumber = 1,
    [FromQuery] int PageSize = 50);

public record DeviceActivityLogEntry(
    Guid Id,
    Guid DeviceId,
    DateTimeOffset Timestamp,
    string ActivityType,
    string Description,
    string Severity,
    string? Metadata);

public record GetDeviceActivityLogResponse(
    IReadOnlyCollection<DeviceActivityLogEntry> Logs,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages);

/// <summary>
/// Handler for retrieving device activity log.
/// </summary>
public class GetDeviceActivityLogHandler
{
    private readonly IDeviceActivityLogQueryRepository _queryRepository;

    public GetDeviceActivityLogHandler(IDeviceActivityLogQueryRepository queryRepository)
    {
        _queryRepository = queryRepository;
    }

    public async Task<Result<GetDeviceActivityLogResponse>> Handle(
        GetDeviceActivityLogQuery query,
        CancellationToken cancellationToken)
    {
        if (query.PageNumber < 1 || query.PageSize < 1 || query.PageSize > 100)
        {
            var error = Error.Validation(
                "INVALID_PAGINATION",
                "Page number must be >= 1 and page size must be between 1 and 100.");
            return Result.Failure<GetDeviceActivityLogResponse>(error);
        }

        var deviceId = new DeviceId(query.DeviceId);

        var (logs, totalCount) = await _queryRepository.GetActivityLogsAsync(
            deviceId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        var logEntries = logs.Select(l => new DeviceActivityLogEntry(
            Id: l.Id,
            DeviceId: l.DeviceId.Value,
            Timestamp: l.Timestamp,
            ActivityType: l.ActivityType,
            Description: l.Description,
            Severity: l.Severity,
            Metadata: l.Metadata
        )).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

        return Result<GetDeviceActivityLogResponse>.Success(new GetDeviceActivityLogResponse(
            Logs: logEntries,
            TotalCount: totalCount,
            PageNumber: query.PageNumber,
            PageSize: query.PageSize,
            TotalPages: totalPages));
    }
}
