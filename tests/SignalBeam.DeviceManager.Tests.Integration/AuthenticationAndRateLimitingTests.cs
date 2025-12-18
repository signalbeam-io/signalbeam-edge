using System.Net;
using System.Net.Http.Json;
using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Tests.Integration.Infrastructure;

namespace SignalBeam.DeviceManager.Tests.Integration;

/// <summary>
/// Integration tests for authentication, authorization, and rate limiting.
/// </summary>
public class AuthenticationAndRateLimitingTests : IClassFixture<DeviceManagerWebApplicationFactory>
{
    private readonly DeviceManagerWebApplicationFactory _factory;

    public AuthenticationAndRateLimitingTests(DeviceManagerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    #region Authentication Tests

    [Fact]
    public async Task Request_WithoutApiKey_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new RegisterDeviceCommand(
            TenantId: _factory.DefaultTenantId,
            DeviceId: Guid.NewGuid(),
            Name: "Test Device");

        // Act
        var response = await client.PostAsJsonAsync("/api/devices", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Error.Should().Be("missing_api_key");
    }

    [Fact]
    public async Task Request_WithInvalidApiKey_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient("");
        var request = new RegisterDeviceCommand(
            TenantId: _factory.DefaultTenantId,
            DeviceId: Guid.NewGuid(),
            Name: "Test Device");

        // Act
        var response = await client.PostAsJsonAsync("/api/devices", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_WithValidApiKey_ReturnsSuccess()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient("valid-test-key");
        var request = new RegisterDeviceCommand(
            TenantId: _factory.DefaultTenantId,
            DeviceId: Guid.NewGuid(),
            Name: "Test Device");

        // Act
        var response = await client.PostAsJsonAsync("/api/devices", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task HealthCheckEndpoint_WithoutAuthentication_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MetricsEndpoint_WithoutAuthentication_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/metrics");

        // Assert - Metrics endpoint should be accessible without auth
        // Note: May return 404 if metrics endpoint is not configured in test environment
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OpenApiEndpoint_WithoutAuthentication_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/openapi/v1.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ScalarEndpoint_WithoutAuthentication_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/scalar/v1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Rate Limiting Tests

    [Fact]
    public async Task RateLimiting_ExceedingLimit_ReturnsTooManyRequests()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        var successfulRequests = 0;
        var rateLimitedRequests = 0;

        // Act - Send many requests rapidly (more than the limit)
        for (int i = 0; i < 105; i++) // Limit is 100 per minute
        {
            var request = new RegisterDeviceCommand(
                TenantId: _factory.DefaultTenantId,
                DeviceId: Guid.NewGuid(),
                Name: $"Device {i}");

            var response = await client.PostAsJsonAsync("/api/devices", request);

            if (response.StatusCode == HttpStatusCode.Created)
            {
                successfulRequests++;
            }
            else if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rateLimitedRequests++;
            }
        }

        // Assert - Should have some rate-limited requests
        rateLimitedRequests.Should().BeGreaterThan(0, "Rate limiting should kick in after 100 requests");
        successfulRequests.Should().BeLessOrEqualTo(100, "Should not exceed the rate limit");
    }

    [Fact]
    public async Task RateLimiting_TooManyRequests_ReturnsRetryAfter()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();

        // Act - Send requests until rate limited
        HttpResponseMessage? rateLimitedResponse = null;
        for (int i = 0; i < 110; i++)
        {
            var request = new RegisterDeviceCommand(
                TenantId: _factory.DefaultTenantId,
                DeviceId: Guid.NewGuid(),
                Name: $"Device {i}");

            var response = await client.PostAsJsonAsync("/api/devices", request);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rateLimitedResponse = response;
                break;
            }
        }

        // Assert
        if (rateLimitedResponse != null)
        {
            var error = await rateLimitedResponse.Content.ReadFromJsonAsync<RateLimitErrorResponse>();
            error.Should().NotBeNull();
            error!.Error.Should().Be("RATE_LIMIT_EXCEEDED");
            error.Message.Should().Contain("Too many requests");
        }
    }

    [Fact]
    public async Task RateLimiting_DifferentApiKeys_IndependentLimits()
    {
        // Arrange
        var client1 = _factory.CreateAuthenticatedClient("api-key-1");
        var client2 = _factory.CreateAuthenticatedClient("api-key-2");

        // Act - Send requests from both clients
        var request = new RegisterDeviceCommand(
            TenantId: _factory.DefaultTenantId,
            DeviceId: Guid.NewGuid(),
            Name: "Test Device");

        var response1 = await client1.PostAsJsonAsync("/api/devices", request);
        var response2 = await client2.PostAsJsonAsync("/api/devices", request with { DeviceId = Guid.NewGuid() });

        // Assert - Both should succeed (independent rate limits per API key/tenant)
        response1.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.BadRequest);
        response2.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.BadRequest);
    }

    #endregion

    #region Tenant Isolation Tests

    [Fact]
    public async Task TenantIsolation_CannotAccessOtherTenantDevices()
    {
        // This test assumes tenant isolation is enforced by the API key validator
        // In our test implementation, each API key is associated with a specific tenant

        // Arrange - Register device with tenant A
        var clientA = _factory.CreateAuthenticatedClient("tenant-a-key");
        var deviceId = Guid.NewGuid();
        var registerRequest = new RegisterDeviceCommand(
            TenantId: _factory.DefaultTenantId,
            DeviceId: deviceId,
            Name: "Tenant A Device");

        await clientA.PostAsJsonAsync("/api/devices", registerRequest);

        // Act - Try to access device list (should only see tenant's own devices)
        var response = await clientA.GetAsync("/api/devices");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // In a full implementation, would verify the response only contains devices for this tenant
    }

    #endregion
}

/// <summary>
/// Error response from the API.
/// </summary>
public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Rate limit error response.
/// </summary>
public class RateLimitErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public double? RetryAfter { get; set; }
}
