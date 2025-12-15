using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Queries;

public record GetDeviceHealthQuery(Guid DeviceId);

public record DeviceHealthResponse(
    Guid DeviceId,
    string Name,
    string Status,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset RegisteredAt,
    bool IsOnline,
    TimeSpan? TimeSinceLastSeen,
    string HealthStatus);

/// <summary>
/// Handler for retrieving device health information.
/// </summary>
public class GetDeviceHealthHandler
{
    private readonly IDeviceQueryRepository _queryRepository;

    public GetDeviceHealthHandler(IDeviceQueryRepository queryRepository)
    {
        _queryRepository = queryRepository;
    }

    public async Task<Result<DeviceHealthResponse>> Handle(
        GetDeviceHealthQuery query,
        CancellationToken cancellationToken)
    {
        var deviceId = new DeviceId(query.DeviceId);
        var device = await _queryRepository.GetByIdAsync(deviceId, cancellationToken);

        if (device is null)
        {
            var error = Error.NotFound("DEVICE_NOT_FOUND", $"Device with ID {query.DeviceId} not found.");
            return Result.Failure<DeviceHealthResponse>(error);
        }

        var isOnline = device.Status == SignalBeam.Domain.Enums.DeviceStatus.Online;
        var now = DateTimeOffset.UtcNow;
        var timeSinceLastSeen = device.LastSeenAt.HasValue
            ? now - device.LastSeenAt.Value
            : (TimeSpan?)null;

        // Determine health status based on last seen time
        var healthStatus = DetermineHealthStatus(device.Status, timeSinceLastSeen);

        return Result<DeviceHealthResponse>.Success(new DeviceHealthResponse(
            DeviceId: device.Id.Value,
            Name: device.Name,
            Status: device.Status.ToString(),
            LastSeenAt: device.LastSeenAt,
            RegisteredAt: device.RegisteredAt,
            IsOnline: isOnline,
            TimeSinceLastSeen: timeSinceLastSeen,
            HealthStatus: healthStatus));
    }

    private static string DetermineHealthStatus(
        SignalBeam.Domain.Enums.DeviceStatus status,
        TimeSpan? timeSinceLastSeen)
    {
        if (status == SignalBeam.Domain.Enums.DeviceStatus.Error)
            return "Unhealthy";

        if (status == SignalBeam.Domain.Enums.DeviceStatus.Offline)
            return "Offline";

        if (status == SignalBeam.Domain.Enums.DeviceStatus.Online)
        {
            if (timeSinceLastSeen.HasValue)
            {
                // If last seen more than 5 minutes ago, mark as degraded
                if (timeSinceLastSeen.Value.TotalMinutes > 5)
                    return "Degraded";

                return "Healthy";
            }

            return "Healthy";
        }

        if (status == SignalBeam.Domain.Enums.DeviceStatus.Updating)
            return "Updating";

        return "Unknown";
    }
}
