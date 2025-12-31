using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Application.Queries;
using SignalBeam.DeviceManager.Tests.Integration.Infrastructure;
using SignalBeam.Domain.Enums;

namespace SignalBeam.DeviceManager.Tests.Integration;

/// <summary>
/// Integration tests for bulk operations on groups.
/// </summary>
public class BulkOperationsIntegrationTests : IClassFixture<DeviceManagerWebApplicationFactory>
{
    private readonly DeviceManagerWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public BulkOperationsIntegrationTests(DeviceManagerWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateAuthenticatedClient();
    }

    #region Bulk Add Tags

    [Fact]
    public async Task BulkAddDeviceTags_ToStaticGroup_AddsTagToAllDevicesInGroup()
    {
        // Arrange - Create static group with multiple devices
        var groupRequest = new CreateDeviceGroupCommand(
            TenantId: _factory.DefaultTenantId,
            Name: "Bulk Add Tags Test Group",
            Type: GroupType.Static);

        var groupResponse = await _client.PostAsJsonAsync("/api/groups", groupRequest);
        var group = await groupResponse.Content.ReadFromJsonAsync<CreateDeviceGroupResponse>();

        // Create and add devices to group
        var device1Id = await CreateTestDeviceAsync("bulk-add-device-1");
        var device2Id = await CreateTestDeviceAsync("bulk-add-device-2");
        var device3Id = await CreateTestDeviceAsync("bulk-add-device-3");

        await _client.PostAsJsonAsync($"/api/groups/{group!.DeviceGroupId}/devices", new { DeviceId = device1Id });
        await _client.PostAsJsonAsync($"/api/groups/{group.DeviceGroupId}/devices", new { DeviceId = device2Id });
        await _client.PostAsJsonAsync($"/api/groups/{group.DeviceGroupId}/devices", new { DeviceId = device3Id });

        // Act - Bulk add tag to all devices in group
        var bulkAddRequest = new { Tag = "bulk-test=added" };
        var bulkAddResponse = await _client.PostAsJsonAsync(
            $"/api/groups/{group.DeviceGroupId}/bulk/add-tags?tenantId={_factory.DefaultTenantId}",
            bulkAddRequest);

        // Assert
        bulkAddResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var bulkAddResult = await bulkAddResponse.Content.ReadFromJsonAsync<BulkAddDeviceTagsResponse>();
        bulkAddResult.Should().NotBeNull();
        bulkAddResult!.DevicesUpdated.Should().Be(3);

        // Verify all devices have the tag
        await VerifyDeviceHasTag(device1Id, "bulk-test=added");
        await VerifyDeviceHasTag(device2Id, "bulk-test=added");
        await VerifyDeviceHasTag(device3Id, "bulk-test=added");
    }

    [Fact]
    public async Task BulkAddDeviceTags_WithNoDevicesInGroup_ReturnsZeroDevicesUpdated()
    {
        // Arrange - Create empty group
        var groupRequest = new CreateDeviceGroupCommand(
            TenantId: _factory.DefaultTenantId,
            Name: "Empty Group for Bulk Add",
            Type: GroupType.Static);

        var groupResponse = await _client.PostAsJsonAsync("/api/groups", groupRequest);
        var group = await groupResponse.Content.ReadFromJsonAsync<CreateDeviceGroupResponse>();

        // Act - Bulk add tag to empty group
        var bulkAddRequest = new { Tag = "test-tag=value" };
        var bulkAddResponse = await _client.PostAsJsonAsync(
            $"/api/groups/{group!.DeviceGroupId}/bulk/add-tags?tenantId={_factory.DefaultTenantId}",
            bulkAddRequest);

        // Assert
        bulkAddResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var bulkAddResult = await bulkAddResponse.Content.ReadFromJsonAsync<BulkAddDeviceTagsResponse>();
        bulkAddResult!.DevicesUpdated.Should().Be(0);
    }

    [Fact]
    public async Task BulkAddDeviceTags_WithInvalidTag_ReturnsBadRequest()
    {
        // Arrange
        var groupRequest = new CreateDeviceGroupCommand(
            TenantId: _factory.DefaultTenantId,
            Name: "Test Group",
            Type: GroupType.Static);

        var groupResponse = await _client.PostAsJsonAsync("/api/groups", groupRequest);
        var group = await groupResponse.Content.ReadFromJsonAsync<CreateDeviceGroupResponse>();

        // Act - Try to add invalid tag
        var bulkAddRequest = new { Tag = "invalid tag with spaces" };
        var bulkAddResponse = await _client.PostAsJsonAsync(
            $"/api/groups/{group!.DeviceGroupId}/bulk/add-tags?tenantId={_factory.DefaultTenantId}",
            bulkAddRequest);

        // Assert
        bulkAddResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Bulk Remove Tags

    [Fact]
    public async Task BulkRemoveDeviceTags_FromStaticGroup_RemovesTagFromAllDevicesInGroup()
    {
        // Arrange - Create group with devices that have a common tag
        var groupRequest = new CreateDeviceGroupCommand(
            TenantId: _factory.DefaultTenantId,
            Name: "Bulk Remove Tags Test Group",
            Type: GroupType.Static);

        var groupResponse = await _client.PostAsJsonAsync("/api/groups", groupRequest);
        var group = await groupResponse.Content.ReadFromJsonAsync<CreateDeviceGroupResponse>();

        // Create devices and add them to group with a common tag
        var device1Id = await CreateTestDeviceAsync("bulk-remove-device-1");
        var device2Id = await CreateTestDeviceAsync("bulk-remove-device-2");
        var device3Id = await CreateTestDeviceAsync("bulk-remove-device-3");

        await _client.PostAsJsonAsync($"/api/groups/{group!.DeviceGroupId}/devices", new { DeviceId = device1Id });
        await _client.PostAsJsonAsync($"/api/groups/{group.DeviceGroupId}/devices", new { DeviceId = device2Id });
        await _client.PostAsJsonAsync($"/api/groups/{group.DeviceGroupId}/devices", new { DeviceId = device3Id });

        // Add common tag to all devices
        var tagToRemove = "temporary=tag";
        await _client.PostAsJsonAsync($"/api/devices/{device1Id}/tags", new AddDeviceTagCommand(device1Id, tagToRemove));
        await _client.PostAsJsonAsync($"/api/devices/{device2Id}/tags", new AddDeviceTagCommand(device2Id, tagToRemove));
        await _client.PostAsJsonAsync($"/api/devices/{device3Id}/tags", new AddDeviceTagCommand(device3Id, tagToRemove));

        // Verify tag exists
        await VerifyDeviceHasTag(device1Id, tagToRemove);

        // Act - Bulk remove tag from all devices in group
        var bulkRemoveRequest = new { Tag = tagToRemove };
        var bulkRemoveResponse = await _client.PostAsJsonAsync(
            $"/api/groups/{group.DeviceGroupId}/bulk/remove-tags?tenantId={_factory.DefaultTenantId}",
            bulkRemoveRequest);

        // Assert
        bulkRemoveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var bulkRemoveResult = await bulkRemoveResponse.Content.ReadFromJsonAsync<BulkRemoveDeviceTagsResponse>();
        bulkRemoveResult.Should().NotBeNull();
        bulkRemoveResult!.DevicesUpdated.Should().Be(3);

        // Verify tag was removed from all devices
        await VerifyDeviceDoesNotHaveTag(device1Id, tagToRemove);
        await VerifyDeviceDoesNotHaveTag(device2Id, tagToRemove);
        await VerifyDeviceDoesNotHaveTag(device3Id, tagToRemove);
    }

    [Fact]
    public async Task BulkRemoveDeviceTags_WhenSomeDevicesDontHaveTag_StillSucceeds()
    {
        // Arrange - Create group with devices, only some have the tag
        var groupRequest = new CreateDeviceGroupCommand(
            TenantId: _factory.DefaultTenantId,
            Name: "Partial Tag Group",
            Type: GroupType.Static);

        var groupResponse = await _client.PostAsJsonAsync("/api/groups", groupRequest);
        var group = await groupResponse.Content.ReadFromJsonAsync<CreateDeviceGroupResponse>();

        var device1Id = await CreateTestDeviceAsync("partial-tag-device-1");
        var device2Id = await CreateTestDeviceAsync("partial-tag-device-2");

        await _client.PostAsJsonAsync($"/api/groups/{group!.DeviceGroupId}/devices", new { DeviceId = device1Id });
        await _client.PostAsJsonAsync($"/api/groups/{group.DeviceGroupId}/devices", new { DeviceId = device2Id });

        // Only add tag to first device
        var tagToRemove = "partial=tag";
        await _client.PostAsJsonAsync($"/api/devices/{device1Id}/tags", new AddDeviceTagCommand(device1Id, tagToRemove));

        // Act - Bulk remove tag
        var bulkRemoveRequest = new { Tag = tagToRemove };
        var bulkRemoveResponse = await _client.PostAsJsonAsync(
            $"/api/groups/{group.DeviceGroupId}/bulk/remove-tags?tenantId={_factory.DefaultTenantId}",
            bulkRemoveRequest);

        // Assert
        bulkRemoveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var bulkRemoveResult = await bulkRemoveResponse.Content.ReadFromJsonAsync<BulkRemoveDeviceTagsResponse>();
        bulkRemoveResult!.DevicesUpdated.Should().Be(2); // Both devices processed, even if only one had the tag
    }

    #endregion

    #region Large Scale Operations

    [Fact]
    public async Task BulkAddDeviceTags_WithManyDevices_CompletesSuccessfully()
    {
        // Arrange - Create group with many devices (simulating real-world scenario)
        var groupRequest = new CreateDeviceGroupCommand(
            TenantId: _factory.DefaultTenantId,
            Name: "Large Scale Test Group",
            Type: GroupType.Static);

        var groupResponse = await _client.PostAsJsonAsync("/api/groups", groupRequest);
        var group = await groupResponse.Content.ReadFromJsonAsync<CreateDeviceGroupResponse>();

        // Create 20 devices and add to group
        var deviceIds = new List<Guid>();
        for (int i = 0; i < 20; i++)
        {
            var deviceId = await CreateTestDeviceAsync($"scale-test-device-{i}");
            deviceIds.Add(deviceId);
            await _client.PostAsJsonAsync($"/api/groups/{group!.DeviceGroupId}/devices", new { DeviceId = deviceId });
        }

        // Act - Bulk add tag to all 20 devices
        var bulkAddRequest = new { Tag = "scale-test=large" };
        var bulkAddResponse = await _client.PostAsJsonAsync(
            $"/api/groups/{group!.DeviceGroupId}/bulk/add-tags?tenantId={_factory.DefaultTenantId}",
            bulkAddRequest);

        // Assert
        bulkAddResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var bulkAddResult = await bulkAddResponse.Content.ReadFromJsonAsync<BulkAddDeviceTagsResponse>();
        bulkAddResult!.DevicesUpdated.Should().Be(20);

        // Verify a sample of devices
        await VerifyDeviceHasTag(deviceIds[0], "scale-test=large");
        await VerifyDeviceHasTag(deviceIds[10], "scale-test=large");
        await VerifyDeviceHasTag(deviceIds[19], "scale-test=large");
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task BulkAddDeviceTags_ToNonExistentGroup_ReturnsNotFound()
    {
        // Arrange
        var nonExistentGroupId = Guid.NewGuid();

        // Act
        var bulkAddRequest = new { Tag = "test=tag" };
        var bulkAddResponse = await _client.PostAsJsonAsync(
            $"/api/groups/{nonExistentGroupId}/bulk/add-tags?tenantId={_factory.DefaultTenantId}",
            bulkAddRequest);

        // Assert
        bulkAddResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BulkRemoveDeviceTags_ToNonExistentGroup_ReturnsNotFound()
    {
        // Arrange
        var nonExistentGroupId = Guid.NewGuid();

        // Act
        var bulkRemoveRequest = new { Tag = "test=tag" };
        var bulkRemoveResponse = await _client.PostAsJsonAsync(
            $"/api/groups/{nonExistentGroupId}/bulk/remove-tags?tenantId={_factory.DefaultTenantId}",
            bulkRemoveRequest);

        // Assert
        bulkRemoveResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

    private async Task VerifyDeviceHasTag(Guid deviceId, string tag)
    {
        var deviceResponse = await _client.GetAsync($"/api/devices/{deviceId}?tenantId={_factory.DefaultTenantId}");
        var device = await deviceResponse.Content.ReadFromJsonAsync<GetDeviceByIdResponse>();
        device!.Tags.Should().Contain(tag);
    }

    private async Task VerifyDeviceDoesNotHaveTag(Guid deviceId, string tag)
    {
        var deviceResponse = await _client.GetAsync($"/api/devices/{deviceId}?tenantId={_factory.DefaultTenantId}");
        var device = await deviceResponse.Content.ReadFromJsonAsync<GetDeviceByIdResponse>();
        device!.Tags.Should().NotContain(tag);
    }

    #endregion
}
