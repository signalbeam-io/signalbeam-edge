using System.Net;
using System.Net.Http.Json;
using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Application.Queries;
using SignalBeam.DeviceManager.Tests.Integration.Infrastructure;

namespace SignalBeam.DeviceManager.Tests.Integration;

/// <summary>
/// Integration tests for Device API endpoints using HTTP requests.
/// </summary>
public class DeviceEndpointsTests : IClassFixture<DeviceManagerWebApplicationFactory>
{
    private readonly DeviceManagerWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DeviceEndpointsTests(DeviceManagerWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task RegisterDevice_WithValidRequest_ReturnsCreated()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var request = new RegisterDeviceCommand(
            TenantId: _factory.DefaultTenantId,
            DeviceId: deviceId,
            Name: "Test Device",
            Metadata: "{\"location\":\"lab\"}");

        // Act
        var response = await _client.PostAsJsonAsync("/api/devices", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain($"/api/devices/{deviceId}");

        var result = await response.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        result.Should().NotBeNull();
        result!.DeviceId.Should().Be(deviceId);
        result.Name.Should().Be("Test Device");
    }

    [Fact]
    public async Task RegisterDevice_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();
        var request = new RegisterDeviceCommand(
            TenantId: _factory.DefaultTenantId,
            DeviceId: Guid.NewGuid(),
            Name: "Test Device");

        // Act
        var response = await unauthenticatedClient.PostAsJsonAsync("/api/devices", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RegisterDevice_WithDuplicateId_ReturnsBadRequest()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var firstRequest = new RegisterDeviceCommand(
            TenantId: _factory.DefaultTenantId,
            DeviceId: deviceId,
            Name: "First Device");

        var secondRequest = new RegisterDeviceCommand(
            TenantId: _factory.DefaultTenantId,
            DeviceId: deviceId,
            Name: "Duplicate Device");

        // Act - Register first device
        await _client.PostAsJsonAsync("/api/devices", firstRequest);

        // Act - Try to register duplicate
        var response = await _client.PostAsJsonAsync("/api/devices", secondRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetDeviceById_WithExistingDevice_ReturnsOk()
    {
        // Arrange - Register a device first
        var deviceId = Guid.NewGuid();
        var registerRequest = new RegisterDeviceCommand(
            TenantId: _factory.DefaultTenantId,
            DeviceId: deviceId,
            Name: "Test Device");

        await _client.PostAsJsonAsync("/api/devices", registerRequest);

        // Act
        var response = await _client.GetAsync($"/api/devices/{deviceId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var device = await response.Content.ReadFromJsonAsync<DeviceResponse>();
        device.Should().NotBeNull();
        device!.Id.Should().Be(deviceId);
        device.Name.Should().Be("Test Device");
    }

    [Fact]
    public async Task GetDeviceById_WithNonExistentDevice_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/devices/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDevices_ReturnsOkWithList()
    {
        // Arrange - Register multiple devices
        var deviceIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        foreach (var deviceId in deviceIds)
        {
            var request = new RegisterDeviceCommand(
                TenantId: _factory.DefaultTenantId,
                DeviceId: deviceId,
                Name: $"Device {deviceId}");

            await _client.PostAsJsonAsync("/api/devices", request);
        }

        // Act
        var response = await _client.GetAsync("/api/devices");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetDevicesResponse>();
        result.Should().NotBeNull();
        result!.Devices.Should().HaveCountGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task GetDevices_WithPagination_ReturnsCorrectPage()
    {
        // Arrange - Register devices
        for (int i = 0; i < 5; i++)
        {
            var request = new RegisterDeviceCommand(
                TenantId: _factory.DefaultTenantId,
                DeviceId: Guid.NewGuid(),
                Name: $"Device {i}");

            await _client.PostAsJsonAsync("/api/devices", request);
        }

        // Act
        var response = await _client.GetAsync("/api/devices?pageNumber=1&pageSize=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetDevicesResponse>();
        result.Should().NotBeNull();
        result!.Devices.Should().HaveCountLessOrEqualTo(2);
    }

    [Fact]
    public async Task UpdateDevice_WithValidRequest_ReturnsOk()
    {
        // Arrange - Register a device
        var deviceId = Guid.NewGuid();
        var registerRequest = new RegisterDeviceCommand(
            TenantId: _factory.DefaultTenantId,
            DeviceId: deviceId,
            Name: "Original Name");

        await _client.PostAsJsonAsync("/api/devices", registerRequest);

        // Act
        var updateRequest = new UpdateDeviceCommand(
            DeviceId: deviceId,
            Name: "Updated Name",
            Metadata: "{\"updated\":true}");

        var response = await _client.PutAsJsonAsync($"/api/devices/{deviceId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var device = await response.Content.ReadFromJsonAsync<UpdateDeviceResponse>();
        device.Should().NotBeNull();
        device!.Name.Should().Be("Updated Name");
        device.Metadata.Should().Be("{\"updated\":true}");
    }

    [Fact]
    public async Task UpdateDevice_WithNonExistentDevice_ReturnsBadRequest()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var updateRequest = new UpdateDeviceCommand(
            DeviceId: nonExistentId,
            Name: "Updated Name");

        // Act
        var response = await _client.PutAsJsonAsync($"/api/devices/{nonExistentId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
