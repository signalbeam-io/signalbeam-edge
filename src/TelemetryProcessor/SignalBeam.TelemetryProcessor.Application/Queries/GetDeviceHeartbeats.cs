using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;
using SignalBeam.TelemetryProcessor.Application.Repositories;

namespace SignalBeam.TelemetryProcessor.Application.Queries;

/// <summary>
/// Query to get device heartbeat history.
/// </summary>
public record GetDeviceHeartbeatsQuery(
    Guid DeviceId,
    DateTimeOffset? StartTime = null,
    DateTimeOffset? EndTime = null);

/// <summary>
/// Response containing device heartbeats.
/// </summary>
public record GetDeviceHeartbeatsResponse(
    Guid DeviceId,
    IReadOnlyCollection<DeviceHeartbeatDto> Heartbeats);

/// <summary>
/// Device heartbeat data transfer object.
/// </summary>
public record DeviceHeartbeatDto(
    Guid Id,
    Guid DeviceId,
    DateTimeOffset Timestamp,
    string Status,
    string? IpAddress,
    string? AdditionalData);

/// <summary>
/// Handler for GetDeviceHeartbeatsQuery.
/// </summary>
public class GetDeviceHeartbeatsHandler
{
    private readonly IDeviceHeartbeatRepository _heartbeatRepository;

    public GetDeviceHeartbeatsHandler(IDeviceHeartbeatRepository heartbeatRepository)
    {
        _heartbeatRepository = heartbeatRepository;
    }

    public async Task<Result<GetDeviceHeartbeatsResponse>> Handle(
        GetDeviceHeartbeatsQuery query,
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
            return Result.Failure<GetDeviceHeartbeatsResponse>(error);
        }

        // Get heartbeats
        var heartbeats = await _heartbeatRepository.GetByDeviceIdAndTimeRangeAsync(
            deviceId,
            startTime,
            endTime,
            cancellationToken);

        // Map to DTOs
        var heartbeatDtos = heartbeats.Select(h => new DeviceHeartbeatDto(
            h.Id,
            h.DeviceId.Value,
            h.Timestamp,
            h.Status,
            h.IpAddress,
            h.AdditionalData)).ToList();

        return Result<GetDeviceHeartbeatsResponse>.Success(new GetDeviceHeartbeatsResponse(
            deviceId.Value,
            heartbeatDtos));
    }
}
