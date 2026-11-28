using SignalBeam.BundleOrchestrator.Application.Services;
using SignalBeam.Shared.Infrastructure.Authentication;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Host.Endpoints;

// Operator endpoints act only within the caller's tenant (#498). The tenant comes from the
// principal's tenant_id claim; tenantId query or body values are ignored. A caller without the
// claim gets 403 (Zitadel JWTs get one with #492).
// Marks endpoints that require the caller's tenant; a test checks every operator endpoint has it.
public sealed class TenantScopedMetadata;

public static class TenantScoping
{
    public static Guid? CallerTenant(HttpContext context) =>
        Guid.TryParse(context.User.FindFirst(AuthenticationConstants.TenantIdClaimType)?.Value, out var id) && id != Guid.Empty
            ? id
            : null;

    public static IResult TenantRequired() => Results.Json(
        new { error = "TENANT_REQUIRED", message = "The caller has no tenant.", type = ErrorType.Forbidden.ToString() },
        statusCode: StatusCodes.Status403Forbidden);

    public static IResult NotFound(Error error) => Results.NotFound(
        new { error = error.Code, message = error.Message, type = error.Type.ToString() });

    // Rejects callers without a tenant before the handler runs.
    public static TBuilder RequireCallerTenant<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder
            .WithMetadata(new TenantScopedMetadata())
            .AddEndpointFilter(async (invocation, next) =>
                CallerTenant(invocation.HttpContext) is null ? TenantRequired() : await next(invocation));

    // The {bundleId} route value must be one of the caller's bundles.
    public static TBuilder RequireOwnBundle<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireOwned("bundleId", (ownership, tenant, id, ct) => ownership.CheckBundleAsync(tenant, id, ct));

    // The {rolloutId} route value must be one of the caller's phased rollouts.
    public static TBuilder RequireOwnPhasedRollout<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireOwned("rolloutId", (ownership, tenant, id, ct) => ownership.CheckPhasedRolloutAsync(tenant, id, ct));

    // The {rolloutId} route value must be one of the caller's /api/rollouts rollouts.
    public static TBuilder RequireOwnRollout<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireOwned("rolloutId", (ownership, tenant, id, ct) => ownership.CheckRolloutAsync(tenant, id, ct));

    // PUT /api/rollouts/{rolloutId}/status addresses a single device status row by its ID.
    public static TBuilder RequireOwnRolloutStatus<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireOwned("rolloutId", (ownership, tenant, id, ct) => ownership.CheckRolloutStatusAsync(tenant, id, ct));

    private static TBuilder RequireOwned<TBuilder>(
        this TBuilder builder,
        string routeKey,
        Func<TenantOwnership, Guid, string?, CancellationToken, Task<Error?>> check)
        where TBuilder : IEndpointConventionBuilder
    {
        // A route without the parameter would skip the check silently, so fail at startup instead.
        builder.Add(endpoint =>
        {
            if (endpoint is RouteEndpointBuilder route && route.RoutePattern.GetParameter(routeKey) is null)
            {
                throw new InvalidOperationException(
                    $"Ownership check on '{route.RoutePattern.RawText}' needs a {{{routeKey}}} route parameter.");
            }
        });

        return builder.AddEndpointFilter(async (invocation, next) =>
        {
            var context = invocation.HttpContext;
            if (CallerTenant(context) is not { } tenant)
            {
                return TenantRequired();
            }

            var error = await check(
                context.RequestServices.GetRequiredService<TenantOwnership>(),
                tenant,
                context.Request.RouteValues[routeKey]?.ToString(),
                context.RequestAborted);
            return error is null ? await next(invocation) : NotFound(error);
        });
    }
}
