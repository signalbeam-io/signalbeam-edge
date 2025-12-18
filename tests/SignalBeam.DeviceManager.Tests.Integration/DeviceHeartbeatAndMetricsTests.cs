using System.Net;
using System.Net.Http.Json;
using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Application.Queries;
using SignalBeam.DeviceManager.Tests.Integration.Infrastructure;

namespace SignalBeam.DeviceManager.Tests.Integration;

/// <summary>
/// Integration tests for Device heartbeat, metrics, and monitoring endpoints.
/// </summary>
public class DeviceHeartbeatAndMetricsTests : IClassFixture<DeviceManagerWebApplicationFactory>
{
    private readonly DeviceManagerWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DeviceHeartbeatAndMetricsTests(DeviceManagerWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task RecordHeartbeat_WithValidRequest_ReturnsOk()
    {
        // Arrange - Register a device first
        var deviceId = await RegisterTestDeviceAsync("Heartbeat Test Device");

        var heartbeatRequest = new RecordHeartbeatCommand(
            DeviceId: deviceId,
            Timestamp: DateTimeOffset.UtcNow);

        // Act
        var response = await _client.PostAsJsonAsync($"/api/devices/{deviceId}/heartbeat", heartbeatRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RecordHeartbeat_WithNonExistentDevice_ReturnsBadRequest()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var heartbeatRequest = new RecordHeartbeatCommand(
            DeviceId: nonExistentId,
            Timestamp: DateTimeOffset.UtcNow);

        // Act
        var response = await _client.PostAsJsonAsync($"/api/devices/{nonExistentId}/heartbeat", heartbeatRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateDeviceMetrics_WithValidRequest_ReturnsOk()
    {
        // Arrange - Register a device
        var deviceId = await RegisterTestDeviceAsync("Metrics Test Device");

        var metricsRequest = new UpdateDeviceMetricsCommand(
            DeviceId: deviceId,
            Timestamp: DateTimeOffset.UtcNow,
            CpuUsage: 45.5,
            MemoryUsage: 60.2,
            DiskUsage: 75.0,
            UptimeSeconds: 3600,
            RunningContainers: 3);

        // Act
        var response = await _client.PostAsJsonAsync($"/api/devices/{deviceId}/metrics", metricsRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetDeviceMetrics_WithExistingMetrics_ReturnsOk()
    {
        // Arrange - Register device and add metrics
        var deviceId = await RegisterTestDeviceAsync("Metrics Query Test");

        var metricsRequest = new UpdateDeviceMetricsCommand(
            DeviceId: deviceId,
            Timestamp: DateTimeOffset.UtcNow,
            CpuUsage: 50.0,
            MemoryUsage: 70.0,
            DiskUsage: 80.0,
            UptimeSeconds: 7200,
            RunningContainers: 5);

        await _client.PostAsJsonAsync($"/api/devices/{deviceId}/metrics", metricsRequest);

        // Act
        var response = await _client.GetAsync($"/api/devices/{deviceId}/metrics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var metrics = await response.Content.ReadFromJsonAsync<GetDeviceMetricsResponse>();
        metrics.Should().NotBeNull();
        metrics!.Metrics.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetDeviceMetrics_WithDateRange_ReturnsFilteredResults()
    {
        // Arrange - Register device and add metrics
        var deviceId = await RegisterTestDeviceAsync("Metrics Filter Test");

        var now = DateTimeOffset.UtcNow;
        var pastMetrics = new UpdateDeviceMetricsCommand(
            DeviceId: deviceId,
            Timestamp: now.AddDays(-2),
            CpuUsage: 30.0,
            MemoryUsage: 40.0,
            DiskUsage: 50.0,
            UptimeSeconds: 86400,
            RunningContainers: 2);

        await _client.PostAsJsonAsync($"/api/devices/{deviceId}/metrics", pastMetrics);

        var from = now.AddDays(-1).ToString("o");
        var to = now.ToString("o");

        // Act
        var response = await _client.GetAsync($"/api/devices/{deviceId}/metrics?from={from}&to={to}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetDeviceHealth_WithHealthyDevice_ReturnsOk()
    {
        // Arrange - Register device and record heartbeat
        var deviceId = await RegisterTestDeviceAsync("Health Test Device");

        var heartbeatRequest = new RecordHeartbeatCommand(
            DeviceId: deviceId,
            Timestamp: DateTimeOffset.UtcNow);

        await _client.PostAsJsonAsync($"/api/devices/{deviceId}/heartbeat", heartbeatRequest);

        // Act
        var response = await _client.GetAsync($"/api/devices/{deviceId}/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var health = await response.Content.ReadFromJsonAsync<DeviceHealthResponse>();
        health.Should().NotBeNull();
        health!.DeviceId.Should().Be(deviceId);
    }

    [Fact]
    public async Task AddDeviceTag_WithValidRequest_ReturnsOk()
    {
        // Arrange - Register a device
        var deviceId = await RegisterTestDeviceAsync("Tag Test Device");

        var tagRequest = new AddDeviceTagCommand(
            DeviceId: deviceId,
            Tag: "production");

        // Act
        var response = await _client.PostAsJsonAsync($"/api/devices/{deviceId}/tags", tagRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AddDeviceTag_MultipleTags_AllPersisted()
    {
        // Arrange - Register a device
        var deviceId = await RegisterTestDeviceAsync("Multi-Tag Test");

        var tags = new[] { "production", "critical", "rpi4" };

        // Act - Add multiple tags
        foreach (var tag in tags)
        {
            var tagRequest = new AddDeviceTagCommand(DeviceId: deviceId, Tag: tag);
            await _client.PostAsJsonAsync($"/api/devices/{deviceId}/tags", tagRequest);
        }

        // Assert - Verify device has all tags
        var response = await _client.GetAsync($"/api/devices/{deviceId}");
        var device = await response.Content.ReadFromJsonAsync<DeviceResponse>();

        device.Should().NotBeNull();
        device!.Tags.Should().Contain("production");
        device.Tags.Should().Contain("critical");
        device.Tags.Should().Contain("rpi4");
    }

    [Fact]
    public async Task ReportDeviceState_WithValidRequest_ReturnsOk()
    {
        // Arrange - Register a device
        var deviceId = await RegisterTestDeviceAsync("State Report Test");

        var stateRequest = new ReportDeviceStateCommand(
            DeviceId: deviceId,
            BundleDeploymentStatus: SignalBeam.Domain.Enums.BundleDeploymentStatus.Completed,
            Timestamp: DateTimeOffset.UtcNow);

        // Act
        var response = await _client.PostAsJsonAsync($"/api/devices/{deviceId}/state", stateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetDeviceActivityLog_ReturnsOk()
    {
        // Arrange - Register a device and perform some actions
        var deviceId = await RegisterTestDeviceAsync("Activity Log Test");

        // Trigger some activity
        await _client.PostAsJsonAsync($"/api/devices/{deviceId}/heartbeat",
            new RecordHeartbeatCommand(deviceId, DateTimeOffset.UtcNow));

        await _client.PostAsJsonAsync($"/api/devices/{deviceId}/tags",
            new AddDeviceTagCommand(deviceId, "test"));

        // Act
        var response = await _client.GetAsync($"/api/devices/{deviceId}/activity-log");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetDeviceActivityLog_WithPagination_ReturnsCorrectPage()
    {
        // Arrange - Register a device
        var deviceId = await RegisterTestDeviceAsync("Activity Pagination Test");

        // Act
        var response = await _client.GetAsync($"/api/devices/{deviceId}/activity-log?pageNumber=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
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
