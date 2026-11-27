using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.BundleOrchestrator.Application.Services;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Queries;

public record GetDeviceDesiredStateQuery(Guid DeviceId);

// The edge agent deserializes this as DesiredState(BundleId, BundleVersion, Containers) from
// SignalBeam.EdgeAgent.Application. With nothing assigned, BundleId is null and Containers is empty:
// the device should run no bundle containers.
public record DeviceDesiredStateResponse(
    Guid DeviceId,
    string? BundleId,
    string? BundleVersion,
    DateTimeOffset? AssignedAt,
    string? AssignedBy,
    IReadOnlyList<DesiredContainerDto> Containers);

public class GetDeviceDesiredStateHandler
{
    private readonly IDeviceDesiredStateRepository _desiredStateRepository;
    private readonly IBundleVersionRepository _bundleVersionRepository;
    private readonly DeviceOwnershipVerifier _ownershipVerifier;

    public GetDeviceDesiredStateHandler(
        IDeviceDesiredStateRepository desiredStateRepository,
        IBundleVersionRepository bundleVersionRepository,
        DeviceOwnershipVerifier ownershipVerifier)
    {
        _desiredStateRepository = desiredStateRepository;
        _bundleVersionRepository = bundleVersionRepository;
        _ownershipVerifier = ownershipVerifier;
    }

    public async Task<Result<DeviceDesiredStateResponse>> Handle(
        GetDeviceDesiredStateQuery query,
        DeviceCaller caller,
        CancellationToken cancellationToken)
    {
        var accessError = await _ownershipVerifier.CheckAsync(query.DeviceId, caller, cancellationToken);
        if (accessError is not null)
        {
            return Result.Failure<DeviceDesiredStateResponse>(accessError);
        }

        var desiredState = await _desiredStateRepository.GetByDeviceIdAsync(new DeviceId(query.DeviceId), cancellationToken);
        if (desiredState is null)
        {
            return Result<DeviceDesiredStateResponse>.Success(
                new DeviceDesiredStateResponse(query.DeviceId, null, null, null, null, []));
        }

        var version = await _bundleVersionRepository.GetByBundleAndVersionAsync(
            desiredState.BundleId, desiredState.BundleVersion, cancellationToken);
        if (version is null)
        {
            // Not "nothing assigned": an empty answer would make the agent stop every container.
            return Result.Failure<DeviceDesiredStateResponse>(Error.Unexpected(
                "DESIRED_BUNDLE_VERSION_MISSING",
                $"The assigned bundle version {desiredState.BundleVersion} could not be found."));
        }

        return Result<DeviceDesiredStateResponse>.Success(new DeviceDesiredStateResponse(
            query.DeviceId,
            desiredState.BundleId.Value.ToString(),
            desiredState.BundleVersion.ToString(),
            desiredState.AssignedAt,
            desiredState.AssignedBy,
            version.Containers.Select(DesiredContainerMapper.Map).ToList()));
    }
}
