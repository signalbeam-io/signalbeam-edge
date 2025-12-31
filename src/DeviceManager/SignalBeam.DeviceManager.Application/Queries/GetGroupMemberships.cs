using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Queries;

/// <summary>
/// Query to get all memberships for a device group.
/// </summary>
public record GetGroupMembershipsQuery(
    Guid TenantId,
    Guid DeviceGroupId);

/// <summary>
/// Response containing group memberships.
/// </summary>
public record GetGroupMembershipsResponse(
    Guid DeviceGroupId,
    string GroupName,
    IReadOnlyCollection<MembershipInfo> Memberships,
    int TotalMemberships,
    int StaticMemberships,
    int DynamicMemberships);

/// <summary>
/// Information about a single group membership.
/// </summary>
public record MembershipInfo(
    Guid MembershipId,
    Guid DeviceId,
    string DeviceName,
    MembershipType Type,
    DateTimeOffset AddedAt,
    string AddedBy);

/// <summary>
/// Type alias with string MembershipType for backward compatibility.
/// </summary>
[Obsolete("Use Type property instead")]
public static class MembershipInfoExtensions
{
    public static string MembershipType(this MembershipInfo info) => info.Type.ToString();
}

/// <summary>
/// Handler for GetGroupMembershipsQuery.
/// </summary>
public class GetGroupMembershipsHandler
{
    private readonly IDeviceGroupRepository _groupRepository;
    private readonly IDeviceGroupMembershipRepository _membershipRepository;
    private readonly IDeviceQueryRepository _deviceRepository;

    public GetGroupMembershipsHandler(
        IDeviceGroupRepository groupRepository,
        IDeviceGroupMembershipRepository membershipRepository,
        IDeviceQueryRepository deviceRepository)
    {
        _groupRepository = groupRepository;
        _membershipRepository = membershipRepository;
        _deviceRepository = deviceRepository;
    }

    public async Task<Result<GetGroupMembershipsResponse>> Handle(
        GetGroupMembershipsQuery query,
        CancellationToken cancellationToken)
    {
        var deviceGroupId = new DeviceGroupId(query.DeviceGroupId);
        var tenantId = new TenantId(query.TenantId);

        // Verify group exists and belongs to tenant
        var deviceGroup = await _groupRepository.GetByIdAsync(deviceGroupId, cancellationToken);
        if (deviceGroup is null)
        {
            var error = Error.NotFound(
                "DEVICE_GROUP_NOT_FOUND",
                $"Device group with ID {query.DeviceGroupId} was not found.");
            return Result.Failure<GetGroupMembershipsResponse>(error);
        }

        if (deviceGroup.TenantId != tenantId)
        {
            var error = Error.Forbidden(
                "DEVICE_GROUP_ACCESS_DENIED",
                "You do not have permission to access this device group.");
            return Result.Failure<GetGroupMembershipsResponse>(error);
        }

        // Get all memberships for the group
        var memberships = await _membershipRepository.GetByGroupIdAsync(deviceGroupId, cancellationToken);

        // Get device information for each membership
        var membershipInfoList = new List<MembershipInfo>();

        foreach (var membership in memberships)
        {
            var device = await _deviceRepository.GetByIdAsync(membership.DeviceId, cancellationToken);

            if (device is not null)
            {
                membershipInfoList.Add(new MembershipInfo(
                    MembershipId: membership.Id.Value,
                    DeviceId: membership.DeviceId.Value,
                    DeviceName: device.Name,
                    Type: membership.Type,
                    AddedAt: membership.AddedAt,
                    AddedBy: membership.AddedBy));
            }
        }

        // Calculate statistics
        var staticCount = membershipInfoList.Count(m => m.Type == MembershipType.Static);
        var dynamicCount = membershipInfoList.Count(m => m.Type == MembershipType.Dynamic);

        var response = new GetGroupMembershipsResponse(
            DeviceGroupId: deviceGroup.Id.Value,
            GroupName: deviceGroup.Name,
            Memberships: membershipInfoList.OrderBy(m => m.DeviceName).ToList(),
            TotalMemberships: membershipInfoList.Count,
            StaticMemberships: staticCount,
            DynamicMemberships: dynamicCount);

        return Result<GetGroupMembershipsResponse>.Success(response);
    }
}
