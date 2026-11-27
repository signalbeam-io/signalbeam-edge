using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SignalBeam.Shared.Infrastructure.Authentication;
using SignalBeam.Shared.Infrastructure.InternalServices;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.Shared.Infrastructure.Tests.Authentication;

// #489: services that register IDeviceCredentialClient accept device keys through UseApiKeyAuthentication.
public class ApiKeyDeviceAuthenticationTests
{
    private const string DeviceKey = "sb_device_abcd1234_secret";

    [Fact]
    public async Task DeviceKey_Valid_SetsDevicePrincipal()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var (context, nextCalled, _) = Build(DeviceKey, DeviceCredentialVerification.Valid(deviceId, tenantId));

        // Act
        await Middleware(nextCalled).InvokeAsync(context);

        // Assert
        nextCalled[0].Should().BeTrue();
        context.User.FindFirst(AuthenticationConstants.DeviceIdClaimType)!.Value.Should().Be(deviceId.ToString());
        context.User.FindFirst(AuthenticationConstants.TenantIdClaimType)!.Value.Should().Be(tenantId.ToString());
        context.User.FindFirst(AuthenticationConstants.AuthMethodClaimType)!.Value
            .Should().Be(AuthenticationConstants.AuthMethodDeviceApiKey);
    }

    [Fact]
    public async Task DeviceKey_Rejected_Returns401()
    {
        // Arrange
        var (context, nextCalled, _) = Build(DeviceKey, DeviceCredentialVerification.Rejected);

        // Act
        await Middleware(nextCalled).InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        nextCalled[0].Should().BeFalse();
    }

    [Fact]
    public async Task DeviceKey_VerificationUnavailable_Returns503()
    {
        // Arrange
        var (context, nextCalled, _) = Build(DeviceKey, DeviceCredentialVerification.Unavailable);

        // Act
        await Middleware(nextCalled).InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        nextCalled[0].Should().BeFalse();
    }

    [Fact]
    public async Task TenantKey_SkipsDeviceVerification()
    {
        // Arrange
        var (context, nextCalled, credentials) = Build("tenant-key", DeviceCredentialVerification.Rejected);
        var tenantId = Guid.NewGuid().ToString();
        var validator = new Mock<IApiKeyValidator>();
        validator.Setup(v => v.ValidateAsync("tenant-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ApiKeyValidationResult>.Success(new ApiKeyValidationResult { TenantId = tenantId }));

        // Act
        await Middleware(nextCalled, validator.Object).InvokeAsync(context);

        // Assert
        nextCalled[0].Should().BeTrue();
        context.User.FindFirst(AuthenticationConstants.AuthMethodClaimType)!.Value
            .Should().Be(AuthenticationConstants.AuthMethodTenantApiKey);
        credentials.Verify(c => c.VerifyApiKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeviceKey_WithoutCredentialClient_FallsThroughToTenantValidator()
    {
        // Arrange: services that don't opt in (e.g. TelemetryProcessor) keep rejecting device keys.
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        context.Request.Path = "/api/alerts";
        context.Request.Headers[AuthenticationConstants.ApiKeyHeaderName] = DeviceKey;
        context.Response.Body = new MemoryStream();
        var nextCalled = new[] { false };
        var validator = new Mock<IApiKeyValidator>();
        validator.Setup(v => v.ValidateAsync(DeviceKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<ApiKeyValidationResult>(Error.Unauthorized("INVALID_API_KEY", "nope")));

        // Act
        await Middleware(nextCalled, validator.Object).InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        nextCalled[0].Should().BeFalse();
    }

    private static ApiKeyAuthenticationMiddleware Middleware(bool[] nextCalled, IApiKeyValidator? validator = null) => new(
        _ => { nextCalled[0] = true; return Task.CompletedTask; },
        validator ?? Mock.Of<IApiKeyValidator>(),
        NullLogger<ApiKeyAuthenticationMiddleware>.Instance);

    private static (HttpContext Context, bool[] NextCalled, Mock<IDeviceCredentialClient> Credentials) Build(
        string apiKey,
        DeviceCredentialVerification verification)
    {
        var credentials = new Mock<IDeviceCredentialClient>();
        credentials.Setup(c => c.VerifyApiKeyAsync(apiKey, It.IsAny<CancellationToken>())).ReturnsAsync(verification);

        var services = new ServiceCollection();
        services.AddSingleton(credentials.Object);

        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        context.Request.Path = "/api/devices/x/desired-state";
        context.Request.Headers[AuthenticationConstants.ApiKeyHeaderName] = apiKey;
        context.Response.Body = new MemoryStream();
        return (context, new[] { false }, credentials);
    }
}
