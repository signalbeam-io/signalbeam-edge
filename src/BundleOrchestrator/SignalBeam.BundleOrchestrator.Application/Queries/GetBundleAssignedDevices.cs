using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Queries;

/// <summary>
/// Query to get all devices assigned to a specific bundle.
/// </summary>
public record GetBundleAssignedDevicesQuery(string BundleId);

/// <summary>
/// DTO for a device assignment.
/// </summary>
public record AssignedDeviceDto(
    Guid DeviceId,
    string BundleVersion,
    DateTimeOffset AssignedAt,
    string? AssignedBy);

/// <summary>
/// Response for GetBundleAssignedDevicesQuery.
/// </summary>
public record GetBundleAssignedDevicesResponse(
    Guid BundleId,
    IReadOnlyList<AssignedDeviceDto> AssignedDevices);

/// <summary>
/// Handler for GetBundleAssignedDevicesQuery.
/// Uses Wolverine's IMessageHandler pattern.
/// </summary>
public class GetBundleAssignedDevicesHandler
{
    private readonly IDeviceDesiredStateRepository _desiredStateRepository;
    private readonly IBundleRepository _bundleRepository;

    public GetBundleAssignedDevicesHandler(
        IDeviceDesiredStateRepository desiredStateRepository,
        IBundleRepository bundleRepository)
    {
        _desiredStateRepository = desiredStateRepository;
        _bundleRepository = bundleRepository;
    }

    public async Task<Result<GetBundleAssignedDevicesResponse>> Handle(
        GetBundleAssignedDevicesQuery query,
        CancellationToken cancellationToken)
    {
        // Parse and validate bundle ID
        if (!BundleId.TryParse(query.BundleId, out var bundleId))
        {
            return Result.Failure<GetBundleAssignedDevicesResponse>(
                Error.Validation("INVALID_BUNDLE_ID", $"Invalid bundle ID format: {query.BundleId}"));
        }

        // Verify bundle exists
        var bundle = await _bundleRepository.GetByIdAsync(bundleId, cancellationToken);
        if (bundle is null)
        {
            return Result.Failure<GetBundleAssignedDevicesResponse>(
                Error.NotFound("BUNDLE_NOT_FOUND", $"Bundle with ID {query.BundleId} not found"));
        }

        // Get all devices with this bundle assigned
        var desiredStates = await _desiredStateRepository.GetByBundleIdAsync(bundleId, cancellationToken);

        // Map to DTOs
        var assignedDevices = desiredStates
            .Select(ds => new AssignedDeviceDto(
                ds.DeviceId.Value,
                ds.BundleVersion.ToString(),
                ds.AssignedAt,
                ds.AssignedBy))
            .OrderByDescending(d => d.AssignedAt)
            .ToList();

        return Result<GetBundleAssignedDevicesResponse>.Success(
            new GetBundleAssignedDevicesResponse(bundleId.Value, assignedDevices));
    }
}
