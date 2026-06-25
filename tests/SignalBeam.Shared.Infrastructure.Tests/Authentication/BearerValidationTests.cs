using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SignalBeam.Shared.Infrastructure.Authentication;

namespace SignalBeam.Shared.Infrastructure.Tests.Authentication;

/// <summary>
/// Regression tests for #422: the device/api-key middlewares used to pass ANY
/// bearer token straight through, so a caller with a bogus token reached
/// endpoints that don't separately enforce authorization. The middlewares must now
/// validate the bearer via the JWT scheme and reject it when validation fails.
/// </summary>
public class BearerValidationTests
{
    private static HttpContext BuildContext(AuthenticateResult bearerResult, out bool[] nextCalledRef)
    {
        var authService = new Mock<IAuthenticationService>();
        authService
            .Setup(s => s.AuthenticateAsync(It.IsAny<HttpContext>(), AuthenticationConstants.JwtBearerScheme))
            .ReturnsAsync(bearerResult);

        var services = new ServiceCollection();
        services.AddSingleton(authService.Object);

        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
        context.Request.Path = "/api/devices";
        context.Request.Headers.Authorization = "Bearer some-token";
        context.Response.Body = new MemoryStream();

        nextCalledRef = new[] { false };
        return context;
    }

    private static AuthenticateResult SuccessfulAuth()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") },
            AuthenticationConstants.JwtBearerScheme));
        return AuthenticateResult.Success(
            new AuthenticationTicket(principal, AuthenticationConstants.JwtBearerScheme));
    }

    // --- DeviceAuthenticationMiddleware ---

    [Fact]
    public async Task DeviceAuth_InvalidBearer_Returns401_AndDoesNotCallNext()
    {
        var context = BuildContext(AuthenticateResult.Fail("invalid signature"), out var nextCalled);
        var middleware = new DeviceAuthenticationMiddleware(
            _ => { nextCalled[0] = true; return Task.CompletedTask; },
            NullLogger<DeviceAuthenticationMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        nextCalled[0].Should().BeFalse();
    }

    [Fact]
    public async Task DeviceAuth_ValidBearer_CallsNext_AndSetsUser()
    {
        var context = BuildContext(SuccessfulAuth(), out var nextCalled);
        var middleware = new DeviceAuthenticationMiddleware(
            _ => { nextCalled[0] = true; return Task.CompletedTask; },
            NullLogger<DeviceAuthenticationMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        nextCalled[0].Should().BeTrue();
        context.User.Identity!.IsAuthenticated.Should().BeTrue();
    }

    // --- ApiKeyAuthenticationMiddleware ---

    [Fact]
    public async Task ApiKeyAuth_InvalidBearer_Returns401_AndDoesNotCallNext()
    {
        var context = BuildContext(AuthenticateResult.Fail("expired"), out var nextCalled);
        var middleware = new ApiKeyAuthenticationMiddleware(
            _ => { nextCalled[0] = true; return Task.CompletedTask; },
            Mock.Of<IApiKeyValidator>(),
            NullLogger<ApiKeyAuthenticationMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        nextCalled[0].Should().BeFalse();
    }

    [Fact]
    public async Task ApiKeyAuth_ValidBearer_CallsNext_AndSetsUser()
    {
        var context = BuildContext(SuccessfulAuth(), out var nextCalled);
        var middleware = new ApiKeyAuthenticationMiddleware(
            _ => { nextCalled[0] = true; return Task.CompletedTask; },
            Mock.Of<IApiKeyValidator>(),
            NullLogger<ApiKeyAuthenticationMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        nextCalled[0].Should().BeTrue();
        context.User.Identity!.IsAuthenticated.Should().BeTrue();
    }
}
