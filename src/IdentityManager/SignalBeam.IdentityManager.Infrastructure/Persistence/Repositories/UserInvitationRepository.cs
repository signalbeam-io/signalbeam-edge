using Microsoft.EntityFrameworkCore;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.IdentityManager.Application.Repositories;

namespace SignalBeam.IdentityManager.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for UserInvitation aggregate.
/// </summary>
public class UserInvitationRepository : IUserInvitationRepository
{
    private readonly IdentityDbContext _context;

    public UserInvitationRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<UserInvitation?> GetByIdAsync(InvitationId id, CancellationToken cancellationToken = default)
    {
        return await _context.UserInvitations
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<UserInvitation?> GetByTokenAsync(Guid token, CancellationToken cancellationToken = default)
    {
        return await _context.UserInvitations
            .FirstOrDefaultAsync(i => i.Token == token, cancellationToken);
    }

    public async Task<UserInvitation?> GetPendingByEmailAndTenantAsync(string email, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.UserInvitations
            .FirstOrDefaultAsync(i => i.Email == email
                && i.TenantId == tenantId
                && i.Status == InvitationStatus.Pending, cancellationToken);
    }

    public async Task<List<UserInvitation>> GetPendingByTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.UserInvitations
            .Where(i => i.TenantId == tenantId && i.Status == InvitationStatus.Pending)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(UserInvitation invitation, CancellationToken cancellationToken = default)
    {
        await _context.UserInvitations.AddAsync(invitation, cancellationToken);
    }

    public Task UpdateAsync(UserInvitation invitation, CancellationToken cancellationToken = default)
    {
        _context.UserInvitations.Update(invitation);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
