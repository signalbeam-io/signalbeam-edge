namespace SignalBeam.Shared.Infrastructure.Authentication.Authorization;

/// <summary>
/// Named authorization policies applied to endpoints via <c>.RequireAuthorization(...)</c>.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Gate for operator / control-plane endpoints (approve/reject devices, mint registration
    /// tokens, bundle CRUD, rollouts). Requires a Zitadel/OIDC JWT; the plaintext tenant API key
    /// only satisfies it under the dev-only escape hatch. Device endpoints (heartbeat,
    /// desired-state, key rotation, certificate flows) are NOT gated by this — they keep the
    /// hashed device API key / mTLS path.
    /// </summary>
    public const string OperatorAccess = "OperatorAccess";
}
