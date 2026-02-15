using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.IdentityManager.Application.Repositories;

/// <summary>
/// Repository interface for UserInvitation aggregate.
/// </summary>
public interface IUserInvitationRepository
{
    Task<UserInvitation?> GetByIdAsync(InvitationId id, CancellationToken cancellationToken = default);
    Task<UserInvitation?> GetByTokenAsync(Guid token, CancellationToken cancellationToken = default);
    Task<UserInvitation?> GetPendingByEmailAndTenantAsync(string email, TenantId tenantId, CancellationToken cancellationToken = default);
    Task<List<UserInvitation>> GetPendingByTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    Task AddAsync(UserInvitation invitation, CancellationToken cancellationToken = default);
    Task UpdateAsync(UserInvitation invitation, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
