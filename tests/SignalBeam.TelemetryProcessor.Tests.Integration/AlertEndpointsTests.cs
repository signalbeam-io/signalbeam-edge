using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.TelemetryProcessor.Tests.Integration.Infrastructure;

namespace SignalBeam.TelemetryProcessor.Tests.Integration;

// The alert API is routed by the public gateway, so it must authenticate and stay inside the
// caller's tenant.
public class AlertEndpointsTests : IClassFixture<TelemetryProcessorWebApplicationFactory>
{
    private readonly TelemetryProcessorWebApplicationFactory _factory;

    public AlertEndpointsTests(TelemetryProcessorWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("GET", "/api/alerts")]
    [InlineData("GET", "/api/alerts/statistics")]
    [InlineData("POST", "/api/alerts/3f0b4c2e-8f8e-4c5e-9a55-7b6f4f0a1c11/resolve")]
    public async Task AlertApi_WithoutCredentials_ReturnsUnauthorized(string method, string path)
    {
        // Act
        var response = await _factory.CreateClient().SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAlerts_ReturnsOnlyTheCallersTenant_EvenWhenAnotherTenantIsRequested()
    {
        // Arrange
        var own = await AddAlertAsync(TelemetryProcessorWebApplicationFactory.TenantA);
        var other = await AddAlertAsync(TelemetryProcessorWebApplicationFactory.TenantB);

        // Act
        var response = await Client(TelemetryProcessorWebApplicationFactory.TenantAKey)
            .GetAsync($"/api/alerts?tenantId={TelemetryProcessorWebApplicationFactory.TenantB}&limit=500");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("alerts").EnumerateArray().Select(a => a.GetProperty("id").GetGuid()).ToList();
        ids.Should().Contain(own).And.NotContain(other);
    }

    [Fact]
    public async Task AlertOfAnotherTenant_IsNotFound_AndCannotBeAcknowledgedOrResolved()
    {
        // Arrange
        var other = await AddAlertAsync(TelemetryProcessorWebApplicationFactory.TenantB);
        var client = Client(TelemetryProcessorWebApplicationFactory.TenantAKey);

        // Act
        var get = await client.GetAsync($"/api/alerts/{other}");
        var acknowledge = await client.PostAsJsonAsync($"/api/alerts/{other}/acknowledge", new { acknowledgedBy = "intruder" });
        var resolve = await client.PostAsync($"/api/alerts/{other}/resolve", null);

        // Assert
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
        acknowledge.StatusCode.Should().Be(HttpStatusCode.NotFound);
        resolve.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await using var db = _factory.GetDbContext();
        (await db.Alerts.FindAsync(other))!.Status.Should().Be(AlertStatus.Active);
    }

    [Fact]
    public async Task AcknowledgeAndResolve_OwnAlert_Succeed()
    {
        // Arrange
        var own = await AddAlertAsync(TelemetryProcessorWebApplicationFactory.TenantA);
        var client = Client(TelemetryProcessorWebApplicationFactory.TenantAKey);

        // Act
        var acknowledge = await client.PostAsJsonAsync($"/api/alerts/{own}/acknowledge", new { acknowledgedBy = "operator" });
        var resolve = await client.PostAsync($"/api/alerts/{own}/resolve", null);

        // Assert
        acknowledge.StatusCode.Should().Be(HttpStatusCode.OK);
        resolve.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private HttpClient Client(string apiKey)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        return client;
    }

    private async Task<Guid> AddAlertAsync(Guid tenantId)
    {
        var alert = Alert.Create(
            new TenantId(tenantId), AlertSeverity.Warning, AlertType.DeviceOffline,
            "Device is offline", "No heartbeat", new DeviceId(Guid.NewGuid()));

        await using var db = _factory.GetDbContext();
        db.Alerts.Add(alert);
        await db.SaveChangesAsync();
        return alert.Id;
    }
}
