using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Tests.Integration.Infrastructure;
using SignalBeam.Shared.Infrastructure.Authentication;

namespace SignalBeam.DeviceManager.Tests.Integration;

public class InternalServiceKeyFactory : DeviceManagerWebApplicationFactory
{
    public const string Key = "test-internal-service-key";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(
            new Dictionary<string, string?> { [InternalServiceAuthentication.ConfigurationKey] = Key }));
    }
}

// Exercises the whole chain: the device auth middleware skips /api/internal, the endpoint is
// anonymous for user/device auth, and the internal service key filter decides.
public class InternalEndpointsTests : IClassFixture<InternalServiceKeyFactory>
{
    private readonly InternalServiceKeyFactory _factory;

    public InternalEndpointsTests(InternalServiceKeyFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetDeviceTenant_WithKey_ReturnsTenant()
    {
        // Arrange
        var deviceId = await RegisterDeviceAsync();
        var client = InternalClient(InternalServiceKeyFactory.Key);

        // Act
        var response = await client.GetAsync($"/api/internal/devices/{deviceId}/tenant");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("tenantId").GetGuid().Should().Be(_factory.DefaultTenantId);
    }

    [Fact]
    public async Task GetDeviceTenant_UnknownDevice_ReturnsNotFound()
    {
        // Act
        var response = await InternalClient(InternalServiceKeyFactory.Key)
            .GetAsync($"/api/internal/devices/{Guid.NewGuid()}/tenant");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key")]
    public async Task GetDeviceTenant_WithoutValidKey_ReturnsUnauthorized(string? key)
    {
        // Arrange
        var deviceId = await RegisterDeviceAsync();

        // Act
        var response = await InternalClient(key).GetAsync($"/api/internal/devices/{deviceId}/tenant");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDeviceTenant_WithTenantApiKeyOnly_ReturnsUnauthorized()
    {
        // Arrange: a tenant credential is not a service credential.
        var deviceId = await RegisterDeviceAsync();

        // Act
        var response = await _factory.CreateAuthenticatedClient().GetAsync($"/api/internal/devices/{deviceId}/tenant");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task VerifyDeviceApiKey_ApprovedDeviceKey_ReturnsDeviceAndTenant()
    {
        // Arrange
        var deviceId = await RegisterDeviceAsync();
        var apiKey = await ApproveDeviceAsync(deviceId);

        // Act
        var response = await InternalClient(InternalServiceKeyFactory.Key)
            .PostAsJsonAsync("/api/internal/device-credentials/verify", new { apiKey });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("deviceId").GetGuid().Should().Be(deviceId);
        body.GetProperty("tenantId").GetGuid().Should().Be(_factory.DefaultTenantId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("test-api-key")]
    [InlineData("sb_device_00000000_notarealsecret")]
    public async Task VerifyDeviceApiKey_InvalidKey_ReturnsUnauthorized(string apiKey)
    {
        // Act
        var response = await InternalClient(InternalServiceKeyFactory.Key)
            .PostAsJsonAsync("/api/internal/device-credentials/verify", new { apiKey });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task VerifyDeviceApiKey_RightPrefixWrongSecret_ReturnsUnauthorized()
    {
        // Arrange
        var deviceId = await RegisterDeviceAsync();
        var apiKey = await ApproveDeviceAsync(deviceId);
        var tampered = apiKey[..^4] + "XXXX";

        // Act
        var response = await InternalClient(InternalServiceKeyFactory.Key)
            .PostAsJsonAsync("/api/internal/device-credentials/verify", new { apiKey = tampered });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task VerifyDeviceApiKey_WithoutServiceKey_ReturnsUnauthorized()
    {
        // Arrange
        var deviceId = await RegisterDeviceAsync();
        var apiKey = await ApproveDeviceAsync(deviceId);

        // Act: a valid device key alone must not be able to call the service endpoint.
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        var response = await client.PostAsJsonAsync("/api/internal/device-credentials/verify", new { apiKey });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<string> ApproveDeviceAsync(Guid deviceId)
    {
        var response = await _factory.CreateAuthenticatedClient().PostAsync($"/api/devices/{deviceId}/approve", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("apiKey").GetString()!;
    }

    [Fact]
    public async Task GetGroupDevices_WithKey_ReturnsMembersOldestFirst()
    {
        // Arrange
        var groupId = await CreateGroupAsync();
        var first = await RegisterDeviceAsync();
        var second = await RegisterDeviceAsync();
        await AddToGroupAsync(groupId, first);
        await AddToGroupAsync(groupId, second);

        // Act
        var response = await InternalClient(InternalServiceKeyFactory.Key)
            .GetAsync($"/api/internal/tenants/{_factory.DefaultTenantId}/groups/{groupId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("groupId").GetGuid().Should().Be(groupId);
        body.GetProperty("deviceIds").EnumerateArray().Select(d => d.GetGuid()).Should().Equal(first, second);
    }

    [Fact]
    public async Task GetGroupDevices_IncludesDevicesAssignedWithPut()
    {
        // Arrange: the simulator puts devices in groups with PUT /api/devices/{id}/group, which
        // sets the device's group but adds no membership row.
        var groupId = await CreateGroupAsync();
        var member = await RegisterDeviceAsync();
        var assigned = await RegisterDeviceAsync();
        await AddToGroupAsync(groupId, member);
        var put = await _factory.CreateAuthenticatedClient().PutAsJsonAsync(
            $"/api/devices/{assigned}/group", new { DeviceId = assigned, DeviceGroupId = groupId });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        var ids = await GroupDeviceIdsAsync(groupId);

        // Assert: memberships first, then devices assigned by group ID.
        ids.Should().Equal(member, assigned);
    }

    [Fact]
    public async Task GetGroupDevices_ExcludesRemovedDevices()
    {
        // Arrange
        var groupId = await CreateGroupAsync();
        var kept = await RegisterDeviceAsync();
        var removed = await RegisterDeviceAsync();
        await AddToGroupAsync(groupId, kept);
        await AddToGroupAsync(groupId, removed);
        var delete = await _factory.CreateAuthenticatedClient().DeleteAsync(
            $"/api/groups/{groupId}/devices/{removed}?tenantId={_factory.DefaultTenantId}");
        delete.IsSuccessStatusCode.Should().BeTrue();

        // Act
        var ids = await GroupDeviceIdsAsync(groupId);

        // Assert
        ids.Should().Equal(kept);
    }

    [Fact]
    public async Task GetGroupDevices_ForAnotherTenant_ReturnsNotFound()
    {
        // Arrange
        var groupId = await CreateGroupAsync();

        // Act
        var response = await InternalClient(InternalServiceKeyFactory.Key)
            .GetAsync($"/api/internal/tenants/{Guid.NewGuid()}/groups/{groupId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetGroupDevices_WithTenantApiKeyOnly_ReturnsUnauthorized()
    {
        // Arrange
        var groupId = await CreateGroupAsync();

        // Act
        var response = await _factory.CreateAuthenticatedClient()
            .GetAsync($"/api/internal/tenants/{_factory.DefaultTenantId}/groups/{groupId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<List<Guid>> GroupDeviceIdsAsync(Guid groupId)
    {
        var response = await InternalClient(InternalServiceKeyFactory.Key)
            .GetAsync($"/api/internal/tenants/{_factory.DefaultTenantId}/groups/{groupId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("deviceIds").EnumerateArray().Select(d => d.GetGuid()).ToList();
    }

    private async Task<Guid> CreateGroupAsync()
    {
        var response = await _factory.CreateAuthenticatedClient().PostAsJsonAsync("/api/groups",
            new CreateDeviceGroupCommand(TenantId: _factory.DefaultTenantId, Name: $"internal-{Guid.NewGuid():N}"[..20]));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreateDeviceGroupResponse>())!.DeviceGroupId;
    }

    private async Task AddToGroupAsync(Guid groupId, Guid deviceId)
    {
        var response = await _factory.CreateAuthenticatedClient()
            .PostAsJsonAsync($"/api/groups/{groupId}/devices", new { DeviceId = deviceId });
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    private HttpClient InternalClient(string? key)
    {
        var client = _factory.CreateClient();
        if (key is not null)
        {
            client.DefaultRequestHeaders.Add(InternalServiceAuthentication.HeaderName, key);
        }

        return client;
    }

    private async Task<Guid> RegisterDeviceAsync()
    {
        var deviceId = Guid.NewGuid();
        var response = await _factory.CreateAuthenticatedClient().PostAsJsonAsync("/api/devices",
            new RegisterDeviceCommand(TenantId: _factory.DefaultTenantId, DeviceId: deviceId, Name: $"internal-{deviceId:N}"[..20]));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return deviceId;
    }
}
