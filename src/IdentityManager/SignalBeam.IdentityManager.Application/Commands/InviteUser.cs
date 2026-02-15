using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.IdentityManager.Application.Repositories;
using SignalBeam.IdentityManager.Application.Services;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.IdentityManager.Application.Commands;

/// <summary>
/// Command to invite a user to a tenant via email.
/// </summary>
public record InviteUserCommand(
    Guid TenantId,
    string Email,
    UserRole Role,
    Guid InvitedByUserId);

/// <summary>
/// Response after successfully creating an invitation.
/// </summary>
public record InviteUserResponse(
    Guid InvitationId,
    string Email,
    string Role,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Handler for inviting a user to a tenant.
/// </summary>
public class InviteUserHandler
{
    private readonly IUserInvitationRepository _invitationRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IEmailService _emailService;

    public InviteUserHandler(
        IUserInvitationRepository invitationRepository,
        IUserRepository userRepository,
        ITenantRepository tenantRepository,
        IEmailService emailService)
    {
        _invitationRepository = invitationRepository;
        _userRepository = userRepository;
        _tenantRepository = tenantRepository;
        _emailService = emailService;
    }

    public async Task<Result<InviteUserResponse>> Handle(
        InviteUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = new TenantId(command.TenantId);

        // Check if already a member
        var existingUsers = await _userRepository.GetByTenantAsync(tenantId, cancellationToken);
        if (existingUsers.Any(u => u.Email.Equals(command.Email, StringComparison.OrdinalIgnoreCase)
            && u.Status != UserStatus.Deleted))
        {
            return Result.Failure<InviteUserResponse>(
                Error.Conflict("USER_ALREADY_MEMBER", "This email is already a member of the tenant."));
        }

        // Check for pending invitation
        var pendingInvitation = await _invitationRepository.GetPendingByEmailAndTenantAsync(
            command.Email, tenantId, cancellationToken);
        if (pendingInvitation != null && !pendingInvitation.IsExpired(DateTimeOffset.UtcNow))
        {
            return Result.Failure<InviteUserResponse>(
                Error.Conflict("INVITATION_PENDING", "A pending invitation already exists for this email."));
        }

        // Get tenant and inviter for email
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<InviteUserResponse>(
                Error.NotFound("TENANT_NOT_FOUND", "Tenant not found."));
        }

        var inviter = await _userRepository.GetByIdAsync(new UserId(command.InvitedByUserId), cancellationToken);
        if (inviter is null)
        {
            return Result.Failure<InviteUserResponse>(
                Error.NotFound("INVITER_NOT_FOUND", "Inviting user not found."));
        }

        // Create invitation (7 day expiry)
        var now = DateTimeOffset.UtcNow;
        var invitation = UserInvitation.Create(
            InvitationId.New(),
            tenantId,
            command.Email,
            command.Role,
            new UserId(command.InvitedByUserId),
            now,
            now.AddDays(7));

        await _invitationRepository.AddAsync(invitation, cancellationToken);
        await _invitationRepository.SaveChangesAsync(cancellationToken);

        // Send invitation email (best-effort, don't fail the command)
        try
        {
            var acceptUrl = $"/invite/accept?token={invitation.Token}";
            await _emailService.SendInvitationEmailAsync(
                command.Email,
                tenant.Name,
                inviter.Name,
                acceptUrl,
                cancellationToken);
        }
        catch
        {
            // Log but don't fail — invitation is persisted
        }

        return Result.Success(new InviteUserResponse(
            invitation.Id.Value,
            invitation.Email,
            invitation.Role.ToString(),
            invitation.ExpiresAt));
    }
}
