using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Queries;

/// <summary>
/// Query to get the desired state for a device.
/// </summary>
public record GetDeviceDesiredStateQuery(string DeviceId);

/// <summary>
/// Device desired state DTO.
/// </summary>
public record DeviceDesiredStateDto(
    Guid DeviceId,
    Guid BundleId,
    string BundleVersion,
    DateTimeOffset AssignedAt,
    string? AssignedBy);

/// <summary>
/// Response for GetDeviceDesiredStateQuery.
/// </summary>
public record GetDeviceDesiredStateResponse(DeviceDesiredStateDto? DesiredState);

/// <summary>
/// Handler for GetDeviceDesiredStateQuery.
/// Uses Wolverine's IMessageHandler pattern.
/// </summary>
public class GetDeviceDesiredStateHandler
{
    private readonly IDeviceDesiredStateRepository _desiredStateRepository;

    public GetDeviceDesiredStateHandler(IDeviceDesiredStateRepository desiredStateRepository)
    {
        _desiredStateRepository = desiredStateRepository;
    }

    public async Task<Result<GetDeviceDesiredStateResponse>> Handle(
        GetDeviceDesiredStateQuery query,
        CancellationToken cancellationToken)
    {
        // Parse and validate device ID
        if (!DeviceId.TryParse(query.DeviceId, out var deviceId))
        {
            return Result.Failure<GetDeviceDesiredStateResponse>(
                Error.Validation("INVALID_DEVICE_ID", $"Invalid device ID format: {query.DeviceId}"));
        }

        // Get desired state
        var desiredState = await _desiredStateRepository.GetByDeviceIdAsync(deviceId, cancellationToken);

        // Map to DTO (null if no desired state exists)
        var desiredStateDto = desiredState is not null
            ? new DeviceDesiredStateDto(
                desiredState.DeviceId.Value,
                desiredState.BundleId.Value,
                desiredState.BundleVersion.ToString(),
                desiredState.AssignedAt,
                desiredState.AssignedBy)
            : null;

        return Result<GetDeviceDesiredStateResponse>.Success(new GetDeviceDesiredStateResponse(desiredStateDto));
    }
}
