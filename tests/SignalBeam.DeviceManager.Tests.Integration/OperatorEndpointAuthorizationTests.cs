using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Tests.Integration.Infrastructure;
using SignalBeam.Shared.Infrastructure.Authentication.Authorization;

namespace SignalBeam.DeviceManager.Tests.Integration;

/// <summary>
/// Verifies the operator-vs-device auth split (#431): operator endpoints require a JWT and the
/// plaintext tenant API key only authorizes them under the dev/test escape hatch, while the device
/// registration handshake and device-key endpoints are unaffected.
/// </summary>
public class OperatorEndpointAuthorizationTests : IClassFixture<DeviceManagerWebApplicationFactory>
{
    private readonly DeviceManagerWebApplicationFactory _factory;

    public OperatorEndpointAuthorizationTests(DeviceManagerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Reconfigures the operator policy with the tenant-key fallback DISABLED, i.e. as it behaves in
    /// Production. ConfigureTestServices runs after the app's own registration, so this later
    /// AddOperatorAuthorization(false) overwrites the "OperatorAccess" policy in the map.
    /// </summary>
    private HttpClient CreateProductionLikeClient(string apiKey = "operator-key")
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddOperatorAuthorization(allowTenantApiKeyFallback: false);
            });
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        return client;
    }

    [Fact]
    public async Task OperatorEndpoint_WithTenantKey_InProduction_IsForbidden()
    {
        // Arrange — a plaintext tenant key that the middleware still validates, but which must NOT
        // authorize an operator endpoint once the dev hatch is off.
        var client = CreateProductionLikeClient();

        // Act — list devices is an operator/dashboard read.
        var response = await client.GetAsync("/api/devices?tenantId=" + _factory.DefaultTenantId);

        // Assert — authenticated (tenant principal set) but not operator-authorized -> 403.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RegistrationTokenMint_WithTenantKey_InProduction_IsForbidden()
    {
        // Minting registration tokens is the highest-value control-plane action — the tenant key
        // must never authorize it outside dev.
        var client = CreateProductionLikeClient();

        var response = await client.PostAsJsonAsync("/api/registration-tokens", new
        {
            tenantId = _factory.DefaultTenantId,
            description = "should be rejected"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OperatorEndpoint_WithTenantKey_InDev_IsAllowed()
    {
        // The default test factory keeps the dev/test escape hatch on, so the tenant key still works
        // for operator endpoints — this is what keeps local dev and the existing suite green.
        var client = _factory.CreateAuthenticatedClient("operator-key");

        var response = await client.GetAsync("/api/devices?tenantId=" + _factory.DefaultTenantId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeviceRegistrationHandshake_StaysOpen_EvenInProduction()
    {
        // The anonymous device handshake is NOT an operator endpoint — ring-fencing the tenant key
        // must not break a brand-new device registering itself.
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
                services.AddOperatorAuthorization(allowTenantApiKeyFallback: false));
        }).CreateClient();

        var response = await client.PostAsJsonAsync("/api/devices", new RegisterDeviceCommand(
            TenantId: _factory.DefaultTenantId,
            DeviceId: Guid.NewGuid(),
            Name: "handshake-device"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task OperatorEndpoint_WithNoCredentials_IsUnauthorized()
    {
        // No credentials at all is rejected by the auth middleware before authorization runs.
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/devices?tenantId=" + _factory.DefaultTenantId);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
