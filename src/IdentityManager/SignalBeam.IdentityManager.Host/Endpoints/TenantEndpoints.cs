using Microsoft.AspNetCore.Mvc;
using SignalBeam.IdentityManager.Application.Queries;

namespace SignalBeam.IdentityManager.Host.Endpoints;

/// <summary>
/// Endpoints for tenant management and information.
/// </summary>
public static class TenantEndpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenants")
            .WithTags("Tenants")
            .WithOpenApi();

        group.MapGet("/retention-policies", GetTenantRetentionPolicies)
            .WithName("GetTenantRetentionPolicies")
            .WithSummary("Get all tenants with their data retention policies")
            .WithDescription("Returns all active tenants with their data retention settings. Used by background workers for data retention enforcement.")
            .Produces<List<TenantRetentionDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> GetTenantRetentionPolicies(
        [FromServices] GetTenantsWithRetentionHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetTenantsWithRetentionQuery();
        var result = await handler.Handle(query, cancellationToken);

        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Failed to fetch tenant retention policies",
                detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }
}
