using System.Net;
using System.Net.Http.Json;
using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Application.Queries;
using SignalBeam.DeviceManager.Tests.Integration.Infrastructure;

namespace SignalBeam.DeviceManager.Tests.Integration;

/// <summary>
/// Integration tests for Device Group API endpoints.
/// </summary>
public class GroupEndpointsTests : IClassFixture<DeviceManagerWebApplicationFactory>
{
    private readonly DeviceManagerWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GroupEndpointsTests(DeviceManagerWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task CreateDeviceGroup_WithValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new CreateDeviceGroupCommand(
            TenantId: _factory.DefaultTenantId,
            Name: "Production Devices",
            Description: "All production edge devices");

        // Act
        var response = await _client.PostAsJsonAsync("/api/groups", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var group = await response.Content.ReadFromJsonAsync<CreateDeviceGroupResponse>();
        group.Should().NotBeNull();
        group!.Name.Should().Be("Production Devices");
        group.Description.Should().Be("All production edge devices");
    }

    [Fact]
    public async Task CreateDeviceGroup_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();
        var request = new CreateDeviceGroupCommand(
            TenantId: _factory.DefaultTenantId,
            Name: "Test Group");

        // Act
        var response = await unauthenticatedClient.PostAsJsonAsync("/api/groups", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDeviceGroups_ReturnsOkWithList()
    {
        // Arrange - Create multiple groups
        var groupNames = new[] { "Group 1", "Group 2", "Group 3" };
        foreach (var name in groupNames)
        {
            var request = new CreateDeviceGroupCommand(
                TenantId: _factory.DefaultTenantId,
                Name: name);

            await _client.PostAsJsonAsync("/api/groups", request);
        }

        // Act
        var response = await _client.GetAsync("/api/groups");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetDeviceGroupsResponse>();
        result.Should().NotBeNull();
        result!.Groups.Should().HaveCountGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task GetDeviceGroups_FilterByTenantId_ReturnsOnlyTenantGroups()
    {
        // Arrange - Create a group
        var request = new CreateDeviceGroupCommand(
            TenantId: _factory.DefaultTenantId,
            Name: "Tenant Specific Group");

        await _client.PostAsJsonAsync("/api/groups", request);

        // Act
        var response = await _client.GetAsync($"/api/groups?tenantId={_factory.DefaultTenantId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetDeviceGroupsResponse>();
        result.Should().NotBeNull();
        result!.Groups.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AddDeviceToGroup_WithValidRequest_ReturnsOk()
    {
        // Arrange - Create a group and register a device
        var groupId = await CreateTestGroupAsync("Test Group");
        var deviceId = await RegisterTestDeviceAsync("Test Device");

        var request = new { DeviceId = deviceId };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/groups/{groupId}/devices", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AddDeviceToGroup_WithNonExistentGroup_ReturnsBadRequest()
    {
        // Arrange
        var nonExistentGroupId = Guid.NewGuid();
        var deviceId = await RegisterTestDeviceAsync("Test Device");

        var request = new { DeviceId = deviceId };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/groups/{nonExistentGroupId}/devices", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddDeviceToGroup_WithNonExistentDevice_ReturnsBadRequest()
    {
        // Arrange
        var groupId = await CreateTestGroupAsync("Test Group");
        var nonExistentDeviceId = Guid.NewGuid();

        var request = new { DeviceId = nonExistentDeviceId };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/groups/{groupId}/devices", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AssignDeviceToGroup_UsingPutEndpoint_ReturnsOk()
    {
        // Arrange - Create a group and register a device
        var groupId = await CreateTestGroupAsync("Assignment Test Group");
        var deviceId = await RegisterTestDeviceAsync("Assignment Test Device");

        var request = new AssignDeviceToGroupCommand(
            DeviceId: deviceId,
            DeviceGroupId: groupId);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/devices/{deviceId}/group", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetDevicesByGroup_WithDevicesInGroup_ReturnsOk()
    {
        // Arrange - Create group and add devices
        var groupId = await CreateTestGroupAsync("Device List Test");
        var deviceIds = new[]
        {
            await RegisterTestDeviceAsync("Device 1"),
            await RegisterTestDeviceAsync("Device 2"),
            await RegisterTestDeviceAsync("Device 3")
        };

        // Add all devices to group
        foreach (var deviceId in deviceIds)
        {
            var request = new { DeviceId = deviceId };
            await _client.PostAsJsonAsync($"/api/groups/{groupId}/devices", request);
        }

        // Act
        var response = await _client.GetAsync($"/api/devices/groups/{groupId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetDevicesByGroupResponse>();
        result.Should().NotBeNull();
        result!.Devices.Should().HaveCountGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task GetDevicesByGroup_WithPagination_ReturnsCorrectPage()
    {
        // Arrange - Create group with multiple devices
        var groupId = await CreateTestGroupAsync("Pagination Test");

        for (int i = 0; i < 5; i++)
        {
            var deviceId = await RegisterTestDeviceAsync($"Device {i}");
            var request = new { DeviceId = deviceId };
            await _client.PostAsJsonAsync($"/api/groups/{groupId}/devices", request);
        }

        // Act
        var response = await _client.GetAsync($"/api/devices/groups/{groupId}?pageNumber=1&pageSize=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetDevicesByGroupResponse>();
        result.Should().NotBeNull();
        result!.Devices.Should().HaveCountLessOrEqualTo(2);
    }

    [Fact]
    public async Task RemoveDeviceFromGroup_ReturnsOk()
    {
        // Arrange - Create group, add device, then remove
        var groupId = await CreateTestGroupAsync("Remove Test");
        var deviceId = await RegisterTestDeviceAsync("Remove Test Device");

        // Add device to group
        var addRequest = new { DeviceId = deviceId };
        await _client.PostAsJsonAsync($"/api/groups/{groupId}/devices", addRequest);

        // Act - Remove device from group (set DeviceGroupId to null)
        var removeRequest = new AssignDeviceToGroupCommand(
            DeviceId: deviceId,
            DeviceGroupId: null);

        var response = await _client.PutAsJsonAsync($"/api/devices/{deviceId}/group", removeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Helper method to create a test device group.
    /// </summary>
    private async Task<Guid> CreateTestGroupAsync(string name)
    {
        var request = new CreateDeviceGroupCommand(
            TenantId: _factory.DefaultTenantId,
            Name: name);

        var response = await _client.PostAsJsonAsync("/api/groups", request);
        var group = await response.Content.ReadFromJsonAsync<CreateDeviceGroupResponse>();

        return group!.DeviceGroupId;
    }

    /// <summary>
    /// Helper method to register a test device.
    /// </summary>
    private async Task<Guid> RegisterTestDeviceAsync(string name)
    {
        var deviceId = Guid.NewGuid();
        var request = new RegisterDeviceCommand(
            TenantId: _factory.DefaultTenantId,
            DeviceId: deviceId,
            Name: name);

        await _client.PostAsJsonAsync("/api/devices", request);
        return deviceId;
    }
}
