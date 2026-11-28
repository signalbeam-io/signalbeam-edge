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
        // TryAddEnumerable so a second call (e.g. a test overriding the policy via ConfigureTestServices
        // to simulate Production) doesn't add a duplicate handler, while handlers for other policies
        // (TryAddSingleton would skip this one if any IAuthorizationHandler were already registered) still do.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, OperatorAccessHandler>());

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

    /// <summary>
    /// Registers the <see cref="AuthorizationPolicies.DeviceAccess"/> and
    /// <see cref="AuthorizationPolicies.DeviceOrOperatorAccess"/> policies and their handler.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="allowTenantApiKeyFallback">Pass <c>!env.IsProduction()</c>, as for operator access.</param>
    public static IServiceCollection AddDeviceAuthorization(
        this IServiceCollection services,
        bool allowTenantApiKeyFallback)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, DeviceAccessHandler>());

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.DeviceAccess, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new DeviceAccessRequirement(allowOperatorJwt: false, allowTenantApiKeyFallback));
            });
            options.AddPolicy(AuthorizationPolicies.DeviceOrOperatorAccess, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new DeviceAccessRequirement(allowOperatorJwt: true, allowTenantApiKeyFallback));
            });
        });

        return services;
    }
}
