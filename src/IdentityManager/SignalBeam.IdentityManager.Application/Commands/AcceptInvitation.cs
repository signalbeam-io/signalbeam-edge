using SignalBeam.IdentityManager.Application.Repositories;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.IdentityManager.Application.Commands;

/// <summary>
/// Command to accept an invitation by token.
/// </summary>
public record AcceptInvitationCommand(Guid Token);

/// <summary>
/// Response after accepting an invitation.
/// </summary>
public record AcceptInvitationResponse(
    Guid TenantId,
    string Email,
    string Role);

/// <summary>
/// Handler for accepting an invitation.
/// </summary>
public class AcceptInvitationHandler
{
    private readonly IUserInvitationRepository _invitationRepository;

    public AcceptInvitationHandler(IUserInvitationRepository invitationRepository)
    {
        _invitationRepository = invitationRepository;
    }

    public async Task<Result<AcceptInvitationResponse>> Handle(
        AcceptInvitationCommand command,
        CancellationToken cancellationToken = default)
    {
        var invitation = await _invitationRepository.GetByTokenAsync(command.Token, cancellationToken);
        if (invitation is null)
        {
            return Result.Failure<AcceptInvitationResponse>(
                Error.NotFound("INVITATION_NOT_FOUND", "Invitation not found."));
        }

        var now = DateTimeOffset.UtcNow;
        if (invitation.IsExpired(now))
        {
            return Result.Failure<AcceptInvitationResponse>(
                Error.Validation("INVITATION_EXPIRED", "This invitation has expired."));
        }

        try
        {
            invitation.Accept(now);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<AcceptInvitationResponse>(
                Error.Validation("INVITATION_INVALID", ex.Message));
        }

        await _invitationRepository.UpdateAsync(invitation, cancellationToken);
        await _invitationRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(new AcceptInvitationResponse(
            invitation.TenantId.Value,
            invitation.Email,
            invitation.Role.ToString()));
    }
}
