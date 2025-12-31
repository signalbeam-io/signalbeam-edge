using Microsoft.Extensions.Logging;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.Queries.TagQuery;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Application.Services;

/// <summary>
/// Service for managing dynamic group memberships.
/// Evaluates tag queries and automatically updates group memberships.
/// </summary>
public class DynamicGroupMembershipManager : IDynamicGroupMembershipManager
{
    private readonly IDeviceGroupRepository _groupRepository;
    private readonly IDeviceQueryRepository _deviceRepository;
    private readonly IDeviceGroupMembershipRepository _membershipRepository;
    private readonly ILogger<DynamicGroupMembershipManager> _logger;

    public DynamicGroupMembershipManager(
        IDeviceGroupRepository groupRepository,
        IDeviceQueryRepository deviceRepository,
        IDeviceGroupMembershipRepository membershipRepository,
        ILogger<DynamicGroupMembershipManager> logger)
    {
        _groupRepository = groupRepository;
        _deviceRepository = deviceRepository;
        _membershipRepository = membershipRepository;
        _logger = logger;
    }

    public async Task UpdateDynamicGroupMembershipsAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var deviceGroupId = new DeviceGroupId(groupId);

        _logger.LogInformation("Updating dynamic group memberships for group {GroupId}", groupId);

        // Get the group
        var group = await _groupRepository.GetByIdAsync(deviceGroupId, cancellationToken);
        if (group is null)
        {
            _logger.LogWarning("Group {GroupId} not found", groupId);
            return;
        }

        // Only process dynamic groups
        if (group.Type != GroupType.Dynamic)
        {
            _logger.LogWarning("Group {GroupId} is not a dynamic group (Type: {Type})", groupId, group.Type);
            return;
        }

        // Parse tag query
        if (string.IsNullOrWhiteSpace(group.TagQuery))
        {
            _logger.LogWarning("Group {GroupId} has no tag query", groupId);
            return;
        }

        TagQueryExpression parsedQuery;
        try
        {
            parsedQuery = TagQueryParser.Parse(group.TagQuery);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse tag query for group {GroupId}: {TagQuery}", groupId, group.TagQuery);
            return;
        }

        // Get all devices for this tenant
        var (devices, _) = await _deviceRepository.GetDevicesAsync(
            group.TenantId.Value,
            status: null,
            tag: null,
            deviceGroupId: null,
            pageNumber: 1,
            pageSize: int.MaxValue,
            cancellationToken);

        // Evaluate tag query against all devices
        var matchingDeviceIds = devices
            .Where(device => TagQueryEvaluator.Evaluate(parsedQuery, device))
            .Select(device => device.Id)
            .ToHashSet();

        _logger.LogInformation(
            "Found {MatchCount} devices matching tag query for group {GroupId}",
            matchingDeviceIds.Count,
            groupId);

        // Get current dynamic memberships for this group
        var currentMemberships = await _membershipRepository.GetByGroupIdAsync(deviceGroupId, cancellationToken);
        var currentDynamicMemberships = currentMemberships
            .Where(m => m.Type == MembershipType.Dynamic)
            .ToList();

        // Determine devices to add and remove
        var currentDeviceIds = currentDynamicMemberships.Select(m => m.DeviceId).ToHashSet();
        var devicesToAdd = matchingDeviceIds.Except(currentDeviceIds).ToList();
        var devicesToRemove = currentDeviceIds.Except(matchingDeviceIds).ToList();

        _logger.LogInformation(
            "Group {GroupId}: Adding {AddCount} devices, removing {RemoveCount} devices",
            groupId,
            devicesToAdd.Count,
            devicesToRemove.Count);

        // Remove devices that no longer match
        if (devicesToRemove.Any())
        {
            var membershipsToRemove = currentDynamicMemberships
                .Where(m => devicesToRemove.Contains(m.DeviceId))
                .ToList();

            await _membershipRepository.RemoveRangeAsync(membershipsToRemove, cancellationToken);
        }

        // Add devices that now match
        if (devicesToAdd.Any())
        {
            var membershipsToAdd = devicesToAdd.Select(deviceId =>
                DeviceGroupMembership.CreateDynamic(
                    DeviceGroupMembershipId.New(),
                    deviceGroupId,
                    deviceId,
                    DateTimeOffset.UtcNow))
                .ToList();

            await _membershipRepository.AddRangeAsync(membershipsToAdd, cancellationToken);
        }

        // Save changes
        await _membershipRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Successfully updated dynamic group memberships for group {GroupId}",
            groupId);
    }

    public Task UpdateAllDynamicGroupsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting update of all dynamic groups");

        // Get all device groups (across all tenants)
        // Note: This requires getting all groups. For multi-tenant optimization,
        // consider processing by tenant
        var tenantDevicesCache = new Dictionary<Guid, List<Device>>();

        // We need to get all tenants' groups
        // For now, we'll get groups by querying the database
        // In a production system, you might want to batch this differently

        _logger.LogWarning("UpdateAllDynamicGroupsAsync requires tenant-specific iteration. Consider refactoring for multi-tenant support.");

        // TODO: Implement proper multi-tenant iteration
        // For now, this method will need to be called per tenant or refactored
        // to work with a tenant repository that can list all tenants

        _logger.LogInformation("Completed update of all dynamic groups");

        return Task.CompletedTask;
    }

    public async Task UpdateDynamicGroupsForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenantIdValue = new TenantId(tenantId);

        _logger.LogInformation("Starting update of dynamic groups for tenant {TenantId}", tenantId);

        // Get all groups for this tenant
        var groups = await _groupRepository.GetByTenantIdAsync(tenantIdValue, cancellationToken);

        // Filter to dynamic groups only
        var dynamicGroups = groups.Where(g => g.Type == GroupType.Dynamic).ToList();

        _logger.LogInformation(
            "Found {DynamicGroupCount} dynamic groups for tenant {TenantId}",
            dynamicGroups.Count,
            tenantId);

        // Update each dynamic group
        foreach (var group in dynamicGroups)
        {
            try
            {
                await UpdateDynamicGroupMembershipsAsync(group.Id.Value, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to update dynamic group {GroupId} for tenant {TenantId}",
                    group.Id.Value,
                    tenantId);
                // Continue with other groups even if one fails
            }
        }

        _logger.LogInformation(
            "Completed update of dynamic groups for tenant {TenantId}",
            tenantId);
    }
}
