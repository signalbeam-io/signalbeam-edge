using Microsoft.EntityFrameworkCore;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.IdentityManager.Application.Repositories;

namespace SignalBeam.IdentityManager.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for Subscription entity.
/// </summary>
public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly IdentityDbContext _context;

    public SubscriptionRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<Subscription?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<Subscription?> GetActiveByTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Subscriptions
            .Where(s => s.TenantId == tenantId)
            .Where(s => s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        await _context.Subscriptions.AddAsync(subscription, cancellationToken);
    }

    public Task UpdateAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        _context.Subscriptions.Update(subscription);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
