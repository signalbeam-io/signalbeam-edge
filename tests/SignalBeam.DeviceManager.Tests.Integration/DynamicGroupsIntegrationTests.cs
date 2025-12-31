using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Application.Queries;
using SignalBeam.DeviceManager.Application.Services;
using SignalBeam.DeviceManager.Tests.Integration.Infrastructure;
using SignalBeam.Domain.Enums;

namespace SignalBeam.DeviceManager.Tests.Integration;

/// <summary>
/// Integration tests for Dynamic Groups and Group Memberships.
/// </summary>
public class DynamicGroupsIntegrationTests : IClassFixture<DeviceManagerWebApplicationFactory>
{
    private readonly DeviceManagerWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DynamicGroupsIntegrationTests(DeviceManagerWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateAuthenticatedClient();
    }

    #region Dynamic Group Creation and Updates

    [Fact]
    public async Task CreateDynamicGroup_WithTagQuery_CreatesGroupSuccessfully()
    {
        // Arrange
        var request = new CreateDeviceGroupCommand(
            TenantId: _factory.DefaultTenantId,
            Name: "Production Warehouse Devices",
            Description: "All production devices in warehouses",
            Type: GroupType.Dynamic,
            TagQuery: "environment=production AND location=warehouse-*");

        // Act
        var response = await _client.PostAsJsonAsync("/api/groups", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var group = await response.Content.ReadFromJsonAsync<CreateDeviceGroupResponse>();
        group.Should().NotBeNull();
        group!.Name.Should().Be("Production Warehouse Devices");
        group.Type.Should().Be(GroupType.Dynamic);
        group.TagQuery.Should().Be("environment=production AND location=warehouse-*");
    }

    [Fact]
    public async Task CreateDynamicGroup_WithoutTagQuery_ReturnsBadRequest()
    {
        // Arrange - Dynamic group must have a tag query
        var request = new CreateDeviceGroupCommand(
            TenantId: _factory.DefaultTenantId,
            Name: "Invalid Dynamic Group",
            Type: GroupType.Dynamic,
            TagQuery: null);

        // Act
        var response = await _client.PostAsJsonAsync("/api/groups", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateDeviceGroup_ChangingTagQuery_UpdatesGroupSuccessfully()
    {
        // Arrange - Create a dynamic group first
        var createRequest = new CreateDeviceGroupCommand(
            TenantId: _factory.DefaultTenantId,
            Name: "Test Dynamic Group",
            Type: GroupType.Dynamic,
            TagQuery: "environment=production");

        var createResponse = await _client.PostAsJsonAsync("/api/groups", createRequest);
        var createdGroup = await createResponse.Content.ReadFromJsonAsync<CreateDeviceGroupResponse>();

        // Act - Update tag query
        var updateRequest = new
        {
            Name = "Updated Dynamic Group",
            Description = "Updated description",
            TagQuery = "environment=production AND hardware=rpi4"
        };

        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/groups/{createdGroup!.DeviceGroupId}?tenantId={_factory.DefaultTenantId}",
            updateRequest);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedGroup = await updateResponse.Content.ReadFromJsonAsync<UpdateDeviceGroupResponse>();
        updatedGroup!.TagQuery.Should().Be("environment=production AND hardware=rpi4");
    }

    #endregion

    #region Dynamic Group Membership Auto-Update

    [Fact]
    public async Task DynamicGroup_AutomaticallyIncludesMatchingDevices_WhenDeviceTagsMatch()
    {
        // Arrange - Create a dynamic group with a tag query
        var groupRequest = new CreateDeviceGroupCommand(
            TenantId: _factory.DefaultTenantId,
            Name: "Production Devices Group",
            Type: GroupType.Dynamic,
            TagQuery: "environment=production");

        var groupResponse = await _client.PostAsJsonAsync("/api/groups", groupRequest);
        var group = await groupResponse.Content.ReadFromJsonAsync<CreateDeviceGroupResponse>();

        // Create devices with matching and non-matching tags
        var prodDevice1Id = await CreateTestDeviceAsync("prod-device-1");
        var prodDevice2Id = await CreateTestDeviceAsync("prod-device-2");
        var devDeviceId = await CreateTestDeviceAsync("dev-device-1");

        await _client.PostAsJsonAsync($"/api/devices/{prodDevice1Id}/tags", new AddDeviceTagCommand(prodDevice1Id, "environment=production"));
        await _client.PostAsJsonAsync($"/api/devices/{prodDevice2Id}/tags", new AddDeviceTagCommand(prodDevice2Id, "environment=production"));
        await _client.PostAsJsonAsync($"/api/devices/{devDeviceId}/tags", new AddDeviceTagCommand(devDeviceId, "environment=dev"));

        // Act - Trigger dynamic group membership update manually
        using var scope = _factory.Services.CreateScope();
        var membershipManager = scope.ServiceProvider.GetRequiredService<IDynamicGroupMembershipManager>();
        await membershipManager.UpdateDynamicGroupMembershipsAsync(group!.DeviceGroupId, CancellationToken.None);

        // Assert - Check group memberships
        var membershipsResponse = await _client.GetAsync($"/api/groups/{group.DeviceGroupId}/memberships?tenantId={_factory.DefaultTenantId}");
        membershipsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var memberships = await membershipsResponse.Content.ReadFromJsonAsync<GetGroupMembershipsResponse>();
        memberships.Should().NotBeNull();
        memberships!.Memberships.Should().HaveCount(2);
        memberships.Memberships.Should().Contain(m => m.DeviceId == prodDevice1Id && m.Type == MembershipType.Dynamic);
        memberships.Memberships.Should().Contain(m => m.DeviceId == prodDevice2Id && m.Type == MembershipType.Dynamic);
        memberships.Memberships.Should().NotContain(m => m.DeviceId == devDeviceId);
    }

    [Fact]
    public async Task DynamicGroup_RemovesDeviceFromGroup_WhenDeviceTagsNoLongerMatch()
    {
        // Arrange - Create dynamic group and matching device
        var groupRequest = new CreateDeviceGroupCommand(
            TenantId: _factory.DefaultTenantId,
            Name: "Staging Devices",
            Type: GroupType.Dynamic,
            TagQuery: "environment=staging");

        var groupResponse = await _client.PostAsJsonAsync("/api/groups", groupRequest);
        var group = await groupResponse.Content.ReadFromJsonAsync<CreateDeviceGroupResponse>();

        var deviceId = await CreateTestDeviceAsync("dynamic-test-device");
        await _client.PostAsJsonAsync($"/api/devices/{deviceId}/tags", new AddDeviceTagCommand(deviceId, "environment=staging"));

        // Trigger initial membership update
        using (var scope = _factory.Services.CreateScope())
        {
            var membershipManager = scope.ServiceProvider.GetRequiredService<IDynamicGroupMembershipManager>();
            await membershipManager.UpdateDynamicGroupMembershipsAsync(group!.DeviceGroupId, CancellationToken.None);
        }

        // Verify device is in group
        var initialMemberships = await _client.GetAsync($"/api/groups/{group!.DeviceGroupId}/memberships?tenantId={_factory.DefaultTenantId}");
        var initialResult = await initialMemberships.Content.ReadFromJsonAsync<GetGroupMembershipsResponse>();
        initialResult!.Memberships.Should().Contain(m => m.DeviceId == deviceId);

        // Act - Change device tag so it no longer matches
        await _client.DeleteAsync($"/api/tags/{deviceId}/tags/environment=staging");
        await _client.PostAsJsonAsync($"/api/devices/{deviceId}/tags", new AddDeviceTagCommand(deviceId, "environment=production"));

        // Trigger membership update again
        using (var scope = _factory.Services.CreateScope())
        {
            var membershipManager = scope.ServiceProvider.GetRequiredService<IDynamicGroupMembershipManager>();
            await membershipManager.UpdateDynamicGroupMembershipsAsync(group.DeviceGroupId, CancellationToken.None);
        }

        // Assert - Device should be removed from group
        var updatedMemberships = await _client.GetAsync($"/api/groups/{group.DeviceGroupId}/memberships?tenantId={_factory.DefaultTenantId}");
        var updatedResult = await updatedMemberships.Content.ReadFromJsonAsync<GetGroupMembershipsResponse>();
        updatedResult!.Memberships.Should().NotContain(m => m.DeviceId == deviceId);
    }

    [Fact]
    public async Task DynamicGroup_WithComplexQuery_IncludesOnlyMatchingDevices()
    {
        // Arrange - Create dynamic group with complex query
        var groupRequest = new CreateDeviceGroupCommand(
            TenantId: _factory.DefaultTenantId,
            Name: "Production RPi Devices in Warehouses",
            Type: GroupType.Dynamic,
            TagQuery: "(hardware=rpi4 OR hardware=rpi5) AND environment=production AND location=warehouse-*");

        var groupResponse = await _client.PostAsJsonAsync("/api/groups", groupRequest);
        var group = await groupResponse.Content.ReadFromJsonAsync<CreateDeviceGroupResponse>();

        // Create devices with various tag combinations
        var matchingDevice1Id = await CreateTestDeviceAsync("matching-1");
        await _client.PostAsJsonAsync($"/api/devices/{matchingDevice1Id}/tags", new AddDeviceTagCommand(matchingDevice1Id, "hardware=rpi4"));
        await _client.PostAsJsonAsync($"/api/devices/{matchingDevice1Id}/tags", new AddDeviceTagCommand(matchingDevice1Id, "environment=production"));
        await _client.PostAsJsonAsync($"/api/devices/{matchingDevice1Id}/tags", new AddDeviceTagCommand(matchingDevice1Id, "location=warehouse-seattle"));

        var matchingDevice2Id = await CreateTestDeviceAsync("matching-2");
        await _client.PostAsJsonAsync($"/api/devices/{matchingDevice2Id}/tags", new AddDeviceTagCommand(matchingDevice2Id, "hardware=rpi5"));
        await _client.PostAsJsonAsync($"/api/devices/{matchingDevice2Id}/tags", new AddDeviceTagCommand(matchingDevice2Id, "environment=production"));
        await _client.PostAsJsonAsync($"/api/devices/{matchingDevice2Id}/tags", new AddDeviceTagCommand(matchingDevice2Id, "location=warehouse-portland"));

        var nonMatchingDevice1Id = await CreateTestDeviceAsync("non-matching-1"); // Wrong hardware
        await _client.PostAsJsonAsync($"/api/devices/{nonMatchingDevice1Id}/tags", new AddDeviceTagCommand(nonMatchingDevice1Id, "hardware=x86"));
        await _client.PostAsJsonAsync($"/api/devices/{nonMatchingDevice1Id}/tags", new AddDeviceTagCommand(nonMatchingDevice1Id, "environment=production"));
        await _client.PostAsJsonAsync($"/api/devices/{nonMatchingDevice1Id}/tags", new AddDeviceTagCommand(nonMatchingDevice1Id, "location=warehouse-austin"));

        var nonMatchingDevice2Id = await CreateTestDeviceAsync("non-matching-2"); // Wrong location
        await _client.PostAsJsonAsync($"/api/devices/{nonMatchingDevice2Id}/tags", new AddDeviceTagCommand(nonMatchingDevice2Id, "hardware=rpi4"));
        await _client.PostAsJsonAsync($"/api/devices/{nonMatchingDevice2Id}/tags", new AddDeviceTagCommand(nonMatchingDevice2Id, "environment=production"));
        await _client.PostAsJsonAsync($"/api/devices/{nonMatchingDevice2Id}/tags", new AddDeviceTagCommand(nonMatchingDevice2Id, "location=office-nyc"));

        // Act - Update memberships
        using var scope = _factory.Services.CreateScope();
        var membershipManager = scope.ServiceProvider.GetRequiredService<IDynamicGroupMembershipManager>();
        await membershipManager.UpdateDynamicGroupMembershipsAsync(group!.DeviceGroupId, CancellationToken.None);

        // Assert
        var membershipsResponse = await _client.GetAsync($"/api/groups/{group.DeviceGroupId}/memberships?tenantId={_factory.DefaultTenantId}");
        var memberships = await membershipsResponse.Content.ReadFromJsonAsync<GetGroupMembershipsResponse>();

        memberships!.Memberships.Should().HaveCount(2);
        memberships.Memberships.Should().Contain(m => m.DeviceId == matchingDevice1Id);
        memberships.Memberships.Should().Contain(m => m.DeviceId == matchingDevice2Id);
        memberships.Memberships.Should().NotContain(m => m.DeviceId == nonMatchingDevice1Id);
        memberships.Memberships.Should().NotContain(m => m.DeviceId == nonMatchingDevice2Id);
    }

    #endregion

    #region Static Group Operations

    [Fact]
    public async Task StaticGroup_ManuallyAddDevice_AddsDeviceSuccessfully()
    {
        // Arrange - Create static group
        var groupRequest = new CreateDeviceGroupCommand(
            TenantId: _factory.DefaultTenantId,
            Name: "Manual Test Group",
            Type: GroupType.Static);

        var groupResponse = await _client.PostAsJsonAsync("/api/groups", groupRequest);
        var group = await groupResponse.Content.ReadFromJsonAsync<CreateDeviceGroupResponse>();

        var deviceId = await CreateTestDeviceAsync("static-group-device");

        // Act - Manually add device to group
        var addRequest = new { DeviceId = deviceId };
        var addResponse = await _client.PostAsJsonAsync($"/api/groups/{group!.DeviceGroupId}/devices", addRequest);

        // Assert
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify membership
        var membershipsResponse = await _client.GetAsync($"/api/groups/{group.DeviceGroupId}/memberships?tenantId={_factory.DefaultTenantId}");
        var memberships = await membershipsResponse.Content.ReadFromJsonAsync<GetGroupMembershipsResponse>();

        memberships!.Memberships.Should().HaveCount(1);
        memberships.Memberships.First().DeviceId.Should().Be(deviceId);
        memberships.Memberships.First().Type.Should().Be(MembershipType.Static);
    }

    [Fact]
    public async Task StaticGroup_RemoveDevice_RemovesDeviceSuccessfully()
    {
        // Arrange - Create static group and add device
        var groupRequest = new CreateDeviceGroupCommand(
            TenantId: _factory.DefaultTenantId,
            Name: "Static Group for Removal Test",
            Type: GroupType.Static);

        var groupResponse = await _client.PostAsJsonAsync("/api/groups", groupRequest);
        var group = await groupResponse.Content.ReadFromJsonAsync<CreateDeviceGroupResponse>();

        var deviceId = await CreateTestDeviceAsync("removal-test-device");
        await _client.PostAsJsonAsync($"/api/groups/{group!.DeviceGroupId}/devices", new { DeviceId = deviceId });

        // Act - Remove device from group
        var removeResponse = await _client.DeleteAsync($"/api/groups/{group.DeviceGroupId}/devices/{deviceId}?tenantId={_factory.DefaultTenantId}");

        // Assert
        removeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify device was removed
        var membershipsResponse = await _client.GetAsync($"/api/groups/{group.DeviceGroupId}/memberships?tenantId={_factory.DefaultTenantId}");
        var memberships = await membershipsResponse.Content.ReadFromJsonAsync<GetGroupMembershipsResponse>();

        memberships!.Memberships.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    private async Task<Guid> CreateTestDeviceAsync(string name)
    {
        var registerCommand = new RegisterDeviceCommand(
            TenantId: _factory.DefaultTenantId,
            Name: name,
            Metadata: null);

        var response = await _client.PostAsJsonAsync("/api/devices/register", registerCommand);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        return result!.DeviceId;
    }

    #endregion
}
