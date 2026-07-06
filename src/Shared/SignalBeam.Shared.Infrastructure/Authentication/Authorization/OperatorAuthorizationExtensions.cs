using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SignalBeam.Shared.Infrastructure.Authentication.Authorization;

/// <summary>
/// DI wiring for the operator-access authorization policy.
/// </summary>
public static class OperatorAuthorizationExtensions
{
    /// <summary>
    /// Registers authorization with the <see cref="AuthorizationPolicies.OperatorAccess"/> policy and
    /// its handler. Replaces the bare <c>services.AddAuthorization()</c> call.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="allowTenantApiKeyFallback">
    /// Pass <c>!env.IsProduction()</c>. When true, the plaintext tenant API key also authorizes
    /// operator endpoints (dev/test escape hatch); in Production only a JWT does.
    /// </param>
    public static IServiceCollection AddOperatorAuthorization(
        this IServiceCollection services,
        bool allowTenantApiKeyFallback)
    {
        // TryAdd so a second call (e.g. a test overriding the policy via ConfigureTestServices to
        // simulate Production) doesn't accumulate duplicate handler instances in the container.
        services.TryAddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, OperatorAccessHandler>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.OperatorAccess, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new OperatorAccessRequirement(allowTenantApiKeyFallback));
            });
        });

        return services;
    }
}
