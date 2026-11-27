using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using SignalBeam.Shared.Infrastructure.Authentication;
using SignalBeam.Shared.Infrastructure.Authentication.Authorization;

namespace SignalBeam.Shared.Infrastructure.Tests.Authentication;

// #489: a device may only read or report for itself; operators and the dev tenant key are opt-in.
public class DeviceAccessHandlerTests
{
    private static readonly Guid RouteDeviceId = Guid.NewGuid();

    [Theory]
    [InlineData(AuthenticationConstants.AuthMethodDeviceApiKey)]
    [InlineData(AuthenticationConstants.AuthMethodCertificate)]
    public async Task DeviceCredential_ForTheRouteDevice_IsAuthorized(string authMethod)
    {
        (await Evaluate(authMethod, RouteDeviceId, allowJwt: false, allowTenantKey: false)).Should().BeTrue();
    }

    [Fact]
    public async Task DeviceCredential_ForAnotherDevice_IsRejected()
    {
        (await Evaluate(AuthenticationConstants.AuthMethodDeviceApiKey, Guid.NewGuid(), allowJwt: true, allowTenantKey: true))
            .Should().BeFalse();
    }

    [Fact]
    public async Task DeviceCredential_WithoutDeviceClaim_IsRejected()
    {
        (await Evaluate(AuthenticationConstants.AuthMethodDeviceApiKey, callerDeviceId: null, allowJwt: false, allowTenantKey: false))
            .Should().BeFalse();
    }

    [Fact]
    public async Task DeviceCredential_WithoutRouteDevice_IsRejected()
    {
        (await Evaluate(AuthenticationConstants.AuthMethodDeviceApiKey, RouteDeviceId, allowJwt: false, allowTenantKey: false, routeDeviceId: "not-a-guid"))
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Jwt_IsAuthorizedOnlyWhenAllowed(bool allowJwt, bool expected)
    {
        (await Evaluate(AuthenticationConstants.AuthMethodJwt, null, allowJwt, allowTenantKey: true)).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task TenantApiKey_IsAuthorizedOnlyUnderTheFallback(bool allowTenantKey, bool expected)
    {
        (await Evaluate(AuthenticationConstants.AuthMethodTenantApiKey, null, allowJwt: true, allowTenantKey)).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("ApiKey")]
    public async Task UnknownOrMissingAuthMethod_IsRejected(string? authMethod)
    {
        (await Evaluate(authMethod, RouteDeviceId, allowJwt: true, allowTenantKey: true)).Should().BeFalse();
    }

    private static async Task<bool> Evaluate(
        string? authMethod,
        Guid? callerDeviceId,
        bool allowJwt,
        bool allowTenantKey,
        string? routeDeviceId = null)
    {
        var claims = new List<Claim>();
        if (authMethod is not null)
        {
            claims.Add(new Claim(AuthenticationConstants.AuthMethodClaimType, authMethod));
        }

        if (callerDeviceId is not null)
        {
            claims.Add(new Claim(AuthenticationConstants.DeviceIdClaimType, callerDeviceId.Value.ToString()));
        }

        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues[DeviceAccessRequirement.DeviceIdRouteValue] = routeDeviceId ?? RouteDeviceId.ToString();

        var requirement = new DeviceAccessRequirement(allowJwt, allowTenantKey);
        var context = new AuthorizationHandlerContext([requirement], user, httpContext);

        await new DeviceAccessHandler().HandleAsync(context);
        return context.HasSucceeded;
    }
}
