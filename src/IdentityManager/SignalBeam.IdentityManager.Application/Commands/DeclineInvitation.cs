using SignalBeam.IdentityManager.Application.Repositories;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.IdentityManager.Application.Commands;

/// <summary>
/// Command to decline an invitation by token.
/// </summary>
public record DeclineInvitationCommand(Guid Token);

/// <summary>
/// Handler for declining an invitation.
/// </summary>
public class DeclineInvitationHandler
{
    private readonly IUserInvitationRepository _invitationRepository;

    public DeclineInvitationHandler(IUserInvitationRepository invitationRepository)
    {
        _invitationRepository = invitationRepository;
    }

    public async Task<Result> Handle(
        DeclineInvitationCommand command,
        CancellationToken cancellationToken = default)
    {
        var invitation = await _invitationRepository.GetByTokenAsync(command.Token, cancellationToken);
        if (invitation is null)
        {
            return Result.Failure(
                Error.NotFound("INVITATION_NOT_FOUND", "Invitation not found."));
        }

        try
        {
            invitation.Decline(DateTimeOffset.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(
                Error.Validation("INVITATION_INVALID", ex.Message));
        }

        await _invitationRepository.UpdateAsync(invitation, cancellationToken);
        await _invitationRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
