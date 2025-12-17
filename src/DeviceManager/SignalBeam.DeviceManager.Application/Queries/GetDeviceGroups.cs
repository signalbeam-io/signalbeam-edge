using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;
using Microsoft.AspNetCore.Mvc;

namespace SignalBeam.DeviceManager.Application.Queries;

public record GetDeviceGroupsQuery(
    [FromQuery] Guid TenantId);

public record DeviceGroupEntry(
    Guid Id,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    IReadOnlyCollection<string> TagCriteria);

public record GetDeviceGroupsResponse(
    IReadOnlyCollection<DeviceGroupEntry> Groups);

/// <summary>
/// Handler for retrieving device groups for a tenant.
/// </summary>
public class GetDeviceGroupsHandler
{
    private readonly IDeviceGroupRepository _groupRepository;

    public GetDeviceGroupsHandler(IDeviceGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task<Result<GetDeviceGroupsResponse>> Handle(
        GetDeviceGroupsQuery query,
        CancellationToken cancellationToken)
    {
        var tenantId = new TenantId(query.TenantId);

        var groups = await _groupRepository.GetByTenantIdAsync(tenantId, cancellationToken);

        var groupEntries = groups.Select(g => new DeviceGroupEntry(
            Id: g.Id.Value,
            Name: g.Name,
            Description: g.Description,
            CreatedAt: g.CreatedAt,
            TagCriteria: g.TagCriteria
        )).ToList();

        return Result<GetDeviceGroupsResponse>.Success(new GetDeviceGroupsResponse(
            Groups: groupEntries));
    }
}
