using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using SignalBeam.Shared.Infrastructure.Authentication;
using SignalBeam.Shared.Infrastructure.Authentication.Authorization;

namespace SignalBeam.Shared.Infrastructure.Tests.Authentication;

/// <summary>
/// Unit tests for the operator-access gate (#431). Operator endpoints must accept a Zitadel/OIDC
/// JWT, accept the plaintext tenant API key ONLY under the dev/test escape hatch, and never accept a
/// device credential or an anonymous caller — a device must not be able to approve devices or mint
/// registration tokens.
/// </summary>
public class OperatorAccessHandlerTests
{
    private static async Task<bool> Evaluate(string? authMethod, bool allowTenantApiKeyFallback)
    {
        var claims = authMethod is null
            ? Array.Empty<Claim>()
            : new[] { new Claim(AuthenticationConstants.AuthMethodClaimType, authMethod) };

        // An authenticated identity requires a non-null authentication type.
        var identity = new ClaimsIdentity(claims, authenticationType: authMethod is null ? null : "test");
        var user = new ClaimsPrincipal(identity);

        var requirement = new OperatorAccessRequirement(allowTenantApiKeyFallback);
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, resource: null);

        await new OperatorAccessHandler().HandleAsync(context);
        return context.HasSucceeded;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Jwt_is_always_authorized(bool fallback)
    {
        (await Evaluate(AuthenticationConstants.AuthMethodJwt, fallback)).Should().BeTrue();
    }

    [Fact]
    public async Task TenantApiKey_is_authorized_only_when_fallback_enabled()
    {
        (await Evaluate(AuthenticationConstants.AuthMethodTenantApiKey, allowTenantApiKeyFallback: true))
            .Should().BeTrue("the dev/test escape hatch accepts the plaintext tenant key");
    }

    [Fact]
    public async Task TenantApiKey_is_rejected_in_production()
    {
        (await Evaluate(AuthenticationConstants.AuthMethodTenantApiKey, allowTenantApiKeyFallback: false))
            .Should().BeFalse("outside dev the plaintext tenant key must not authorize operator endpoints");
    }

    [Theory]
    [InlineData(AuthenticationConstants.AuthMethodDeviceApiKey)]
    [InlineData(AuthenticationConstants.AuthMethodCertificate)]
    public async Task Device_credentials_are_never_authorized(string deviceAuthMethod)
    {
        (await Evaluate(deviceAuthMethod, allowTenantApiKeyFallback: true)).Should().BeFalse();
        (await Evaluate(deviceAuthMethod, allowTenantApiKeyFallback: false)).Should().BeFalse();
    }

    [Fact]
    public async Task Anonymous_or_untagged_principal_is_never_authorized()
    {
        (await Evaluate(authMethod: null, allowTenantApiKeyFallback: true)).Should().BeFalse();
        (await Evaluate(authMethod: null, allowTenantApiKeyFallback: false)).Should().BeFalse();
    }
}
