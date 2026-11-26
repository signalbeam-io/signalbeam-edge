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
