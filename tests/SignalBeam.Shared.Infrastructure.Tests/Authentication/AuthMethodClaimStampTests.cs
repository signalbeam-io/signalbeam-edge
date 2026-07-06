using System.Security.Claims;
using FluentAssertions;
using SignalBeam.Shared.Infrastructure.Authentication;

namespace SignalBeam.Shared.Infrastructure.Tests.Authentication;

/// <summary>
/// The operator-access gate trusts the middleware-stamped <c>auth_method</c> claim. A JWT payload
/// could itself carry that claim (a Zitadel custom claim), so the stamp must be authoritative:
/// FindFirst must return the middleware value, never a pre-existing untrusted one.
/// </summary>
public class AuthMethodClaimStampTests
{
    [Fact]
    public void Apply_overrides_a_preexisting_auth_method_claim_from_the_token()
    {
        // A JWT-style identity that already carries a (hostile) auth_method claim.
        var identity = new ClaimsIdentity(
            new[] { new Claim(AuthenticationConstants.AuthMethodClaimType, AuthenticationConstants.AuthMethodTenantApiKey) },
            authenticationType: "Bearer");
        var principal = new ClaimsPrincipal(identity);

        AuthMethodClaimStamp.Apply(principal, AuthenticationConstants.AuthMethodJwt);

        principal.FindAll(AuthenticationConstants.AuthMethodClaimType)
            .Should().ContainSingle("the stamp must be the only auth_method claim")
            .Which.Value.Should().Be(AuthenticationConstants.AuthMethodJwt);
    }

    [Fact]
    public void Apply_stamps_when_no_claim_exists()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(Array.Empty<Claim>(), "Bearer"));

        AuthMethodClaimStamp.Apply(principal, AuthenticationConstants.AuthMethodJwt);

        principal.FindFirst(AuthenticationConstants.AuthMethodClaimType)!.Value
            .Should().Be(AuthenticationConstants.AuthMethodJwt);
    }
}
