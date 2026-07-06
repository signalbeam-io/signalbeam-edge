using Microsoft.AspNetCore.Authorization;

namespace SignalBeam.Shared.Infrastructure.Authentication.Authorization;

/// <summary>
/// Evaluates <see cref="OperatorAccessRequirement"/> against the caller's authentication method,
/// which the auth middleware records as the <see cref="AuthenticationConstants.AuthMethodClaimType"/>
/// claim. Reading that claim (rather than the ClaimsIdentity scheme name) avoids the "ApiKey"
/// overload between device-specific and tenant keys.
/// </summary>
public sealed class OperatorAccessHandler : AuthorizationHandler<OperatorAccessRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperatorAccessRequirement requirement)
    {
        var authMethod = context.User
            .FindFirst(AuthenticationConstants.AuthMethodClaimType)?.Value;

        var authorized = authMethod switch
        {
            // Operator authenticated via OIDC — always allowed.
            AuthenticationConstants.AuthMethodJwt => true,
            // Plaintext tenant key — allowed only under the dev/test escape hatch.
            AuthenticationConstants.AuthMethodTenantApiKey => requirement.AllowTenantApiKeyFallback,
            // Device credentials (hashed key, mTLS) and anonymous callers can never reach operator
            // endpoints — a device must not be able to approve/reject devices or mint tokens.
            _ => false
        };

        if (authorized)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
