using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SignalBeam.Domain.Enums;
using SignalBeam.IdentityManager.Application.Commands;
using SignalBeam.IdentityManager.Application.Queries;
using SignalBeam.IdentityManager.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.IdentityManager.Host.Endpoints;

/// <summary>
/// Team management endpoints for invitations, members, and roles.
/// </summary>
public static class TeamEndpoints
{
    public static IEndpointRouteBuilder MapTeamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/teams")
            .WithTags("Teams")
            .WithOpenApi()
            .RequireAuthorization();

        // Admin-only endpoints
        group.MapPost("/invite", InviteUser)
            .WithName("InviteUser")
            .WithSummary("Invite a user to the tenant")
            .Produces<InviteUserResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/members", GetMembers)
            .WithName("GetTeamMembers")
            .WithSummary("Get paginated list of team members")
            .Produces<PaginatedTeamResponse>();

        group.MapPut("/members/{userId}/role", ChangeRole)
            .WithName("ChangeUserRole")
            .WithSummary("Change a team member's role")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/members/{userId}", RemoveMember)
            .WithName("RemoveTeamMember")
            .WithSummary("Remove a team member")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/invitations", GetInvitations)
            .WithName("GetPendingInvitations")
            .WithSummary("Get pending invitations for the tenant")
            .Produces<List<InvitationDto>>();

        // Public endpoints — invited users may not have accounts yet
        group.MapPost("/invitations/{token}/accept", AcceptInvitation)
            .WithName("AcceptInvitation")
            .WithSummary("Accept an invitation by token")
            .Produces<AcceptInvitationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        group.MapPost("/invitations/{token}/decline", DeclineInvitation)
            .WithName("DeclineInvitation")
            .WithSummary("Decline an invitation by token")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> InviteUser(
        [FromBody] InviteUserRequest request,
        [FromServices] InviteUserHandler handler,
        [FromServices] IUserRepository userRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (userId, tenantId, error) = await ResolveAdminContext(httpContext, userRepository, cancellationToken);
        if (error != null) return error;

        var command = new InviteUserCommand(tenantId!.Value, request.Email, request.Role, userId!.Value);
        var result = await handler.Handle(command, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/teams/invitations/{result.Value.InvitationId}", result.Value)
            : MapError(result.Error!);
    }

    private static async Task<IResult> GetMembers(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] string? search,
        [FromServices] GetTenantUsersHandler handler,
        [FromServices] IUserRepository userRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (_, tenantId, error) = await ResolveCallerContext(httpContext, userRepository, cancellationToken);
        if (error != null) return error;

        var clampedPageSize = pageSize > 0 ? Math.Min(pageSize, 100) : 20;
        var query = new GetTenantUsersQuery(tenantId!.Value, page > 0 ? page : 1, clampedPageSize, search);
        var result = await handler.Handle(query, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : MapError(result.Error!);
    }

    private static async Task<IResult> ChangeRole(
        Guid userId,
        [FromBody] ChangeRoleRequest request,
        [FromServices] ChangeUserRoleHandler handler,
        [FromServices] IUserRepository userRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (callerId, tenantId, error) = await ResolveAdminContext(httpContext, userRepository, cancellationToken);
        if (error != null) return error;

        var command = new ChangeUserRoleCommand(tenantId!.Value, userId, request.Role, callerId!.Value);
        var result = await handler.Handle(command, cancellationToken);

        return result.IsSuccess
            ? Results.NoContent()
            : MapError(result.Error!);
    }

    private static async Task<IResult> RemoveMember(
        Guid userId,
        [FromServices] RemoveUserFromTenantHandler handler,
        [FromServices] IUserRepository userRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (callerId, tenantId, error) = await ResolveAdminContext(httpContext, userRepository, cancellationToken);
        if (error != null) return error;

        var command = new RemoveUserFromTenantCommand(tenantId!.Value, userId, callerId!.Value);
        var result = await handler.Handle(command, cancellationToken);

        return result.IsSuccess
            ? Results.NoContent()
            : MapError(result.Error!);
    }

    private static async Task<IResult> GetInvitations(
        [FromServices] GetPendingInvitationsHandler handler,
        [FromServices] IUserRepository userRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (_, tenantId, error) = await ResolveAdminContext(httpContext, userRepository, cancellationToken);
        if (error != null) return error;

        var query = new GetPendingInvitationsQuery(tenantId!.Value);
        var result = await handler.Handle(query, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : MapError(result.Error!);
    }

    private static async Task<IResult> AcceptInvitation(
        Guid token,
        [FromServices] AcceptInvitationHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new AcceptInvitationCommand(token);
        var result = await handler.Handle(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : MapError(result.Error!);
    }

    private static async Task<IResult> DeclineInvitation(
        Guid token,
        [FromServices] DeclineInvitationHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new DeclineInvitationCommand(token);
        var result = await handler.Handle(command, cancellationToken);

        return result.IsSuccess
            ? Results.NoContent()
            : MapError(result.Error!);
    }

    private static async Task<(Guid? UserId, Guid? TenantId, IResult? Error)> ResolveCallerContext(
        HttpContext httpContext,
        IUserRepository userRepository,
        CancellationToken cancellationToken)
    {
        var zitadelUserId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(zitadelUserId))
        {
            return (null, null, Results.Unauthorized());
        }

        var user = await userRepository.GetByZitadelIdAsync(zitadelUserId, cancellationToken);
        if (user is null)
        {
            return (null, null, Results.NotFound(new { error = "USER_NOT_FOUND", message = "User not found." }));
        }

        return (user.Id.Value, user.TenantId.Value, null);
    }

    private static async Task<(Guid? UserId, Guid? TenantId, IResult? Error)> ResolveAdminContext(
        HttpContext httpContext,
        IUserRepository userRepository,
        CancellationToken cancellationToken)
    {
        var (userId, tenantId, error) = await ResolveCallerContext(httpContext, userRepository, cancellationToken);
        if (error != null) return (userId, tenantId, error);

        var user = await userRepository.GetByIdAsync(new UserId(userId!.Value), cancellationToken);
        if (user is null || user.Role != UserRole.Admin)
        {
            return (null, null, Results.Forbid());
        }

        return (userId, tenantId, null);
    }

    private static IResult MapError(Error error) => error.Type switch
    {
        ErrorType.Validation => Results.BadRequest(new { error = error.Code, message = error.Message }),
        ErrorType.NotFound => Results.NotFound(new { error = error.Code, message = error.Message }),
        ErrorType.Conflict => Results.Conflict(new { error = error.Code, message = error.Message }),
        ErrorType.Forbidden => Results.Forbid(),
        _ => Results.Problem(title: error.Code, detail: error.Message, statusCode: StatusCodes.Status500InternalServerError)
    };
}

public record InviteUserRequest(string Email, UserRole Role);
public record ChangeRoleRequest(UserRole Role);
