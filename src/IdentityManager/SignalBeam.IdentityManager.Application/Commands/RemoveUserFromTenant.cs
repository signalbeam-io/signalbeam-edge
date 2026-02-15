using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.IdentityManager.Application.Repositories;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.IdentityManager.Application.Commands;

/// <summary>
/// Command to remove a user from a tenant (soft-delete).
/// </summary>
public record RemoveUserFromTenantCommand(
    Guid TenantId,
    Guid TargetUserId,
    Guid RequestedByUserId);

/// <summary>
/// Handler for removing a user from a tenant.
/// </summary>
public class RemoveUserFromTenantHandler
{
    private readonly IUserRepository _userRepository;

    public RemoveUserFromTenantHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result> Handle(
        RemoveUserFromTenantCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.TargetUserId == command.RequestedByUserId)
        {
            return Result.Failure(
                Error.Validation("CANNOT_REMOVE_SELF", "You cannot remove yourself from the tenant."));
        }

        var tenantId = new TenantId(command.TenantId);
        var targetUserId = new UserId(command.TargetUserId);

        var targetUser = await _userRepository.GetByIdAsync(targetUserId, cancellationToken);
        if (targetUser is null || targetUser.TenantId != tenantId)
        {
            return Result.Failure(
                Error.NotFound("USER_NOT_FOUND", "User not found in this tenant."));
        }

        // Prevent removing the last admin
        if (targetUser.Role == UserRole.Admin)
        {
            var tenantUsers = await _userRepository.GetByTenantAsync(tenantId, cancellationToken);
            var adminCount = tenantUsers.Count(u => u.Role == UserRole.Admin && u.Status == UserStatus.Active);
            if (adminCount <= 1)
            {
                return Result.Failure(
                    Error.Validation("LAST_ADMIN", "Cannot remove the last admin from the tenant."));
            }
        }

        targetUser.Delete();
        await _userRepository.UpdateAsync(targetUser, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
