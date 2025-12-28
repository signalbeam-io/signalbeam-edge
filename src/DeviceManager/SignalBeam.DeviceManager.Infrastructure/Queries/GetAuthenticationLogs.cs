using Microsoft.EntityFrameworkCore;
using SignalBeam.DeviceManager.Infrastructure.Persistence;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Infrastructure.Queries;

/// <summary>
/// Query to get authentication logs for a device.
/// </summary>
public record GetAuthenticationLogsQuery(
    Guid? DeviceId = null,
    DateTimeOffset? StartDate = null,
    DateTimeOffset? EndDate = null,
    bool? SuccessOnly = null,
    int PageNumber = 1,
    int PageSize = 50);

/// <summary>
/// Response containing authentication logs.
/// </summary>
public record GetAuthenticationLogsResponse(
    List<AuthenticationLogDto> Logs,
    int TotalCount,
    int PageNumber,
    int PageSize);

/// <summary>
/// DTO for authentication log entry.
/// </summary>
public record AuthenticationLogDto(
    Guid Id,
    Guid? DeviceId,
    string? IpAddress,
    string? UserAgent,
    bool Success,
    string? FailureReason,
    DateTimeOffset Timestamp,
    string? ApiKeyPrefix);

/// <summary>
/// Handler for GetAuthenticationLogsQuery.
/// </summary>
public class GetAuthenticationLogsHandler
{
    private readonly DeviceDbContext _context;

    public GetAuthenticationLogsHandler(DeviceDbContext context)
    {
        _context = context;
    }

    public async Task<Result<GetAuthenticationLogsResponse>> Handle(
        GetAuthenticationLogsQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var logsQuery = _context.DeviceAuthenticationLogs.AsQueryable();

            // Filter by device ID
            if (query.DeviceId.HasValue)
            {
                var deviceId = new DeviceId(query.DeviceId.Value);
                logsQuery = logsQuery.Where(l => l.DeviceId == deviceId);
            }

            // Filter by start date
            if (query.StartDate.HasValue)
            {
                logsQuery = logsQuery.Where(l => l.Timestamp >= query.StartDate.Value);
            }

            // Filter by end date
            if (query.EndDate.HasValue)
            {
                logsQuery = logsQuery.Where(l => l.Timestamp <= query.EndDate.Value);
            }

            // Filter by success status
            if (query.SuccessOnly.HasValue)
            {
                logsQuery = logsQuery.Where(l => l.Success == query.SuccessOnly.Value);
            }

            // Order by timestamp descending (most recent first)
            logsQuery = logsQuery.OrderByDescending(l => l.Timestamp);

            // Get total count before pagination
            var totalCount = await logsQuery.CountAsync(cancellationToken);

            // Apply pagination
            var logs = await logsQuery
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(l => new AuthenticationLogDto(
                    l.Id,
                    l.DeviceId != null ? l.DeviceId.Value : null,
                    l.IpAddress,
                    l.UserAgent,
                    l.Success,
                    l.FailureReason,
                    l.Timestamp,
                    l.ApiKeyPrefix))
                .ToListAsync(cancellationToken);

            return Result<GetAuthenticationLogsResponse>.Success(
                new GetAuthenticationLogsResponse(
                    logs,
                    totalCount,
                    query.PageNumber,
                    query.PageSize));
        }
        catch (Exception ex)
        {
            return Result.Failure<GetAuthenticationLogsResponse>(
                Error.Unexpected(
                    "AuthenticationLogs.QueryFailed",
                    $"Failed to retrieve authentication logs: {ex.Message}"));
        }
    }
}
