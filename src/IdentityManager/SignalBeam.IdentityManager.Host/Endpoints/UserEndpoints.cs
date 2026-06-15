using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SignalBeam.IdentityManager.Application.Commands;

namespace SignalBeam.IdentityManager.Host.Endpoints;

/// <summary>
/// User profile management endpoints.
/// </summary>
public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization();

        group.MapPut("/me/profile", UpdateProfile)
            .WithName("UpdateUserProfile")
            .WithSummary("Update current user's profile")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/me", DeleteAccount)
            .WithName("DeleteUserAccount")
            .WithSummary("Delete current user's account")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> UpdateProfile(
        [FromBody] UpdateUserProfileRequest request,
        [FromServices] UpdateUserProfileHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var zitadelUserId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(zitadelUserId))
        {
            return Results.Unauthorized();
        }

        var command = new UpdateUserProfileCommand(zitadelUserId, request.Name);
        var result = await handler.Handle(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error!.Type switch
            {
                SignalBeam.Shared.Infrastructure.Results.ErrorType.Validation =>
                    Results.BadRequest(new { error = result.Error.Code, message = result.Error.Message }),
                SignalBeam.Shared.Infrastructure.Results.ErrorType.NotFound =>
                    Results.NotFound(new { error = result.Error.Code, message = result.Error.Message }),
                _ => Results.Problem(
                    title: "Failed to update profile",
                    detail: result.Error.Message,
                    statusCode: StatusCodes.Status500InternalServerError)
            };
        }

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAccount(
        [FromServices] DeleteUserAccountHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var zitadelUserId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(zitadelUserId))
        {
            return Results.Unauthorized();
        }

        var command = new DeleteUserAccountCommand(zitadelUserId);
        var result = await handler.Handle(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error!.Type switch
            {
                SignalBeam.Shared.Infrastructure.Results.ErrorType.NotFound =>
                    Results.NotFound(new { error = result.Error.Code, message = result.Error.Message }),
                _ => Results.Problem(
                    title: "Failed to delete account",
                    detail: result.Error.Message,
                    statusCode: StatusCodes.Status500InternalServerError)
            };
        }

        return Results.NoContent();
    }
}

public record UpdateUserProfileRequest(string Name);
