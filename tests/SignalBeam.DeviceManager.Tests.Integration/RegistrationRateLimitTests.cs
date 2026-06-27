using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SignalBeam.DeviceManager.Host;
using SignalBeam.DeviceManager.Host.Endpoints;
using SignalBeam.DeviceManager.Tests.Integration.Infrastructure;

namespace SignalBeam.DeviceManager.Tests.Integration;

/// <summary>
/// Test factory that tightens the anonymous registration per-IP rate limit so it can be exercised
/// deterministically. The base factory keeps it high so functional tests aren't rate-limited.
/// </summary>
public class RegistrationRateLimitedFactory : DeviceManagerWebApplicationFactory
{
    public const int RegistrationPermit = 5;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Runs after the base ConfigureTestServices, so this value wins.
        builder.ConfigureTestServices(services =>
            services.Configure<RegistrationRateLimitOptions>(o => o.PermitLimit = RegistrationPermit));
    }
}

/// <summary>
/// Verifies that the anonymous device-registration handshake is rate-limited per client IP so it
/// cannot be used to flood a tenant with Pending devices (#430).
/// </summary>
public class RegistrationRateLimitTests : IClassFixture<RegistrationRateLimitedFactory>
{
    private readonly RegistrationRateLimitedFactory _factory;
    private readonly HttpClient _client;

    public RegistrationRateLimitTests(RegistrationRateLimitedFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient(); // unauthenticated — the public registration handshake
    }

    [Fact]
    public async Task RegistrationHandshake_ExceedingPerIpLimit_ReturnsTooManyRequests()
    {
        // Fire well past the per-IP permit concurrently; all requests share the test host's loopback
        // IP, so they land in one registration partition.
        const int totalRequests = 20;

        var responses = await Task.WhenAll(Enumerable.Range(0, totalRequests).Select(i =>
            _client.PostAsJsonAsync("/api/devices", new RegisterDeviceRequest(
                Name: $"rl-device-{i}",
                TenantId: _factory.DefaultTenantId))));

        var created = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var rateLimited = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);

        rateLimited.Should().BeGreaterThan(0, "the per-IP registration limit should reject the flood");
        created.Should().BeLessThanOrEqualTo(RegistrationRateLimitedFactory.RegistrationPermit,
            "successful registrations should not exceed the per-IP permit");
    }
}
