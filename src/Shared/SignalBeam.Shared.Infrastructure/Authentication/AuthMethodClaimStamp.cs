using System.Security.Claims;

namespace SignalBeam.Shared.Infrastructure.Authentication;

/// <summary>
/// Stamps the authoritative <see cref="AuthenticationConstants.AuthMethodClaimType"/> claim on a
/// principal after authentication, so authorization policies can trust it.
/// </summary>
public static class AuthMethodClaimStamp
{
    /// <summary>
    /// Records how the caller authenticated. Any pre-existing <c>auth_method</c> claim is stripped
    /// first so the middleware value is authoritative: a JWT payload could itself carry an
    /// <c>auth_method</c> claim (e.g. a Zitadel custom claim), and <see cref="ClaimsPrincipal.FindFirst"/>
    /// returns the FIRST match — which would be the untrusted payload value, not ours. Fail-closed:
    /// the authorization handler can only ever see the value the middleware decided.
    /// </summary>
    public static void Apply(ClaimsPrincipal principal, string method)
    {
        foreach (var identity in principal.Identities)
        {
            foreach (var existing in identity.FindAll(AuthenticationConstants.AuthMethodClaimType).ToList())
            {
                identity.RemoveClaim(existing);
            }
        }

        if (principal.Identity is ClaimsIdentity primary)
        {
            primary.AddClaim(new Claim(AuthenticationConstants.AuthMethodClaimType, method));
        }
        else
        {
            principal.AddIdentity(new ClaimsIdentity(
                new[] { new Claim(AuthenticationConstants.AuthMethodClaimType, method) }));
        }
    }
}
