using SignalBeam.Domain.Enums;
using SignalBeam.Domain.Events;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.IdentityManager.Application.Repositories;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.IdentityManager.Application.Commands;

/// <summary>
/// Command to change a user's role within a tenant.
/// </summary>
public record ChangeUserRoleCommand(
    Guid TenantId,
    Guid TargetUserId,
    UserRole NewRole,
    Guid RequestedByUserId);

/// <summary>
/// Handler for changing a user's role.
/// </summary>
public class ChangeUserRoleHandler
{
    private readonly IUserRepository _userRepository;

    public ChangeUserRoleHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result> Handle(
        ChangeUserRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = new TenantId(command.TenantId);
        var targetUserId = new UserId(command.TargetUserId);

        var targetUser = await _userRepository.GetByIdAsync(targetUserId, cancellationToken);
        if (targetUser is null || targetUser.TenantId != tenantId)
        {
            return Result.Failure(
                Error.NotFound("USER_NOT_FOUND", "User not found in this tenant."));
        }

        if (targetUser.Role == command.NewRole)
        {
            return Result.Success();
        }

        // Prevent removing the last admin
        if (targetUser.Role == UserRole.Admin && command.NewRole != UserRole.Admin)
        {
            var tenantUsers = await _userRepository.GetByTenantAsync(tenantId, cancellationToken);
            var adminCount = tenantUsers.Count(u => u.Role == UserRole.Admin && u.Status == UserStatus.Active);
            if (adminCount <= 1)
            {
                return Result.Failure(
                    Error.Validation("LAST_ADMIN", "Cannot change the role of the last admin."));
            }
        }

        var oldRole = targetUser.Role;
        targetUser.ChangeRole(command.NewRole);

        await _userRepository.UpdateAsync(targetUser, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
