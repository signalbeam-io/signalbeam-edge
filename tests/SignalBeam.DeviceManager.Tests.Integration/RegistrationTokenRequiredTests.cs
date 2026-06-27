using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SignalBeam.DeviceManager.Host;
using SignalBeam.DeviceManager.Host.Endpoints;
using SignalBeam.DeviceManager.Tests.Integration.Infrastructure;

namespace SignalBeam.DeviceManager.Tests.Integration;

/// <summary>
/// Test factory with the opt-in "require registration token" policy enabled.
/// </summary>
public class TokenRequiredRegistrationFactory : DeviceManagerWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
            services.Configure<DeviceRegistrationOptions>(o => o.RequireRegistrationToken = true));
    }
}

/// <summary>
/// Verifies the opt-in policy (#430) that requires a registration token even for the initial
/// handshake, closing the tokenless anonymous-registration surface.
/// </summary>
public class RegistrationTokenRequiredTests : IClassFixture<TokenRequiredRegistrationFactory>
{
    private readonly TokenRequiredRegistrationFactory _factory;
    private readonly HttpClient _client;

    public RegistrationTokenRequiredTests(TokenRequiredRegistrationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithoutToken_WhenTokenRequired_ReturnsBadRequest()
    {
        var request = new RegisterDeviceRequest(
            Name: "no-token-device",
            TenantId: _factory.DefaultTenantId);

        var response = await _client.PostAsJsonAsync("/api/devices", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Be("REGISTRATION_TOKEN_REQUIRED");
    }
}
