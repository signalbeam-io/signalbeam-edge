using System.Net;
using SignalBeam.TelemetryProcessor.Tests.Integration.Infrastructure;

namespace SignalBeam.TelemetryProcessor.Tests.Integration;

/// <summary>
/// Integration tests for TelemetryProcessor health check endpoints.
/// </summary>
public class HealthCheckTests : IClassFixture<TelemetryProcessorWebApplicationFactory>
{
    private readonly TelemetryProcessorWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public HealthCheckTests(TelemetryProcessorWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Health_Endpoint_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task HealthLive_Endpoint_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task HealthReady_Endpoint_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Root_Endpoint_ReturnsServiceInfo()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("SignalBeam TelemetryProcessor");
        content.Should().Contain("running");
    }
}
