using Microsoft.AspNetCore.Authorization;

namespace SignalBeam.Shared.Infrastructure.Authentication.Authorization;

/// <summary>
/// Requires an operator-grade credential. A Zitadel/OIDC JWT always satisfies it; the config-backed
/// plaintext tenant API key satisfies it only when <see cref="AllowTenantApiKeyFallback"/> is set —
/// the dev/test escape hatch. Device credentials (hashed API key, certificate) never satisfy it.
/// </summary>
public sealed class OperatorAccessRequirement : IAuthorizationRequirement
{
    public OperatorAccessRequirement(bool allowTenantApiKeyFallback)
    {
        AllowTenantApiKeyFallback = allowTenantApiKeyFallback;
    }

    /// <summary>
    /// When true (non-Production), a plaintext tenant API key also authorizes operator endpoints so
    /// local development and the integration test suite work without a running OIDC provider. In
    /// Production this is false, so only a JWT authorizes — the plaintext key is ring-fenced out.
    /// </summary>
    public bool AllowTenantApiKeyFallback { get; }
}
