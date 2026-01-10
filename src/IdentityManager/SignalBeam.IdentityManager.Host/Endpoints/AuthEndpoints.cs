using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SignalBeam.IdentityManager.Application.Commands;
using SignalBeam.IdentityManager.Application.Queries;

namespace SignalBeam.IdentityManager.Host.Endpoints;

/// <summary>
/// Authentication and user registration endpoints.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication")
            .WithOpenApi();

        group.MapPost("/register", RegisterUser)
            .WithName("RegisterUser")
            .WithSummary("Register new user and create tenant")
            .WithDescription("Self-service user registration. Creates user, tenant, and subscription atomically.")
            .Produces<RegisterUserResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/me", GetCurrentUser)
            .WithName("GetCurrentUser")
            .WithSummary("Get current authenticated user")
            .WithDescription("Returns complete user context including tenant and subscription information.")
            .RequireAuthorization()
            .Produces<UserDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    /// <summary>
    /// Register a new user and create their tenant.
    /// POST /api/auth/register
    /// </summary>
    private static async Task<IResult> RegisterUser(
        [FromBody] RegisterUserCommand command,
        [FromServices] RegisterUserHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error!.Type switch
            {
                SignalBeam.Shared.Infrastructure.Results.ErrorType.Validation =>
                    Results.BadRequest(new { error = result.Error.Code, message = result.Error.Message }),
                SignalBeam.Shared.Infrastructure.Results.ErrorType.Conflict =>
                    Results.Conflict(new { error = result.Error.Code, message = result.Error.Message }),
                _ => Results.Problem(
                    title: "Registration failed",
                    detail: result.Error.Message,
                    statusCode: StatusCodes.Status500InternalServerError)
            };
        }

        return Results.Created($"/api/auth/users/{result.Value.UserId}", result.Value);
    }

    /// <summary>
    /// Get current authenticated user information.
    /// GET /api/auth/me
    /// </summary>
    private static async Task<IResult> GetCurrentUser(
        [FromServices] GetCurrentUserHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // Debug: Log all claims
        var claims = httpContext.User.Claims.Select(c => $"{c.Type}={c.Value}");
        Console.WriteLine($"[AUTH] Claims in token: {string.Join(", ", claims)}");
        Console.WriteLine($"[AUTH] User.Identity.IsAuthenticated: {httpContext.User.Identity?.IsAuthenticated}");

        // Extract Zitadel user ID from JWT claims
        // ASP.NET Core maps "sub" claim to ClaimTypes.NameIdentifier
        var zitadelUserId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Console.WriteLine($"[AUTH] Extracted zitadelUserId: {zitadelUserId}");

        if (string.IsNullOrEmpty(zitadelUserId))
        {
            Console.WriteLine("[AUTH] zitadelUserId is null or empty - returning 401");
            return Results.Unauthorized();
        }

        var query = new GetCurrentUserQuery(zitadelUserId);
        var result = await handler.Handle(query, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error!.Type switch
            {
                SignalBeam.Shared.Infrastructure.Results.ErrorType.NotFound =>
                    Results.NotFound(new { error = result.Error.Code, message = result.Error.Message }),
                _ => Results.Problem(
                    title: "Failed to get user",
                    detail: result.Error.Message,
                    statusCode: StatusCodes.Status500InternalServerError)
            };
        }

        return Results.Ok(result.Value);
    }
}
