using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Queries;

public record GetGroupDevicesQuery(Guid TenantId, Guid GroupId);

public record GetGroupDevicesResponse(Guid GroupId, string Name, IReadOnlyList<Guid> DeviceIds);

// Service-to-service lookup used by BundleOrchestrator to target a group's devices (#491).
// A device is in a group through a membership row (POST /api/groups/{id}/devices, dynamic rules)
// or through its DeviceGroupId (PUT /api/devices/{id}/group, used by the simulator). Both count:
// memberships oldest first, then the rest by registration, so phased rollouts pick the same
// devices for each phase.
public class GetGroupDevicesHandler
{
    private readonly IDeviceGroupRepository _groupRepository;
    private readonly IDeviceGroupMembershipRepository _membershipRepository;
    private readonly IDeviceQueryRepository _deviceRepository;

    public GetGroupDevicesHandler(
        IDeviceGroupRepository groupRepository,
        IDeviceGroupMembershipRepository membershipRepository,
        IDeviceQueryRepository deviceRepository)
    {
        _groupRepository = groupRepository;
        _membershipRepository = membershipRepository;
        _deviceRepository = deviceRepository;
    }

    public async Task<Result<GetGroupDevicesResponse>> Handle(GetGroupDevicesQuery query, CancellationToken cancellationToken)
    {
        var groupId = new DeviceGroupId(query.GroupId);
        var group = await _groupRepository.GetByIdAsync(groupId, cancellationToken);

        // Another tenant's group is reported as missing, not forbidden, so IDs can't be probed.
        if (group is null || group.TenantId != new TenantId(query.TenantId))
        {
            return Result.Failure<GetGroupDevicesResponse>(
                Error.NotFound("DEVICE_GROUP_NOT_FOUND", $"Device group with ID {query.GroupId} was not found."));
        }

        var memberships = await _membershipRepository.GetByGroupIdAsync(groupId, cancellationToken);
        var assigned = await _deviceRepository.GetIdsInGroupAsync(group.TenantId, groupId, cancellationToken);
        var deviceIds = memberships
            .OrderBy(m => m.AddedAt)
            .ThenBy(m => m.DeviceId.Value)
            .Select(m => m.DeviceId.Value)
            .Concat(assigned.Select(d => d.Value))
            .Distinct()
            .ToList();

        return Result<GetGroupDevicesResponse>.Success(new GetGroupDevicesResponse(group.Id.Value, group.Name, deviceIds));
    }
}
