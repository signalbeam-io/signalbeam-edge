using Microsoft.EntityFrameworkCore;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IBundleRepository.
/// </summary>
public class BundleRepository : IBundleRepository
{
    private readonly BundleDbContext _context;

    public BundleRepository(BundleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<AppBundle?> GetByIdAsync(BundleId id, CancellationToken cancellationToken = default)
    {
        return await _context.AppBundles
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<AppBundle?> GetByNameAsync(
        TenantId tenantId,
        string name,
        CancellationToken cancellationToken = default)
    {
        return await _context.AppBundles
            .FirstOrDefaultAsync(
                b => b.TenantId == tenantId && b.Name == name,
                cancellationToken);
    }

    public async Task<IReadOnlyList<AppBundle>> GetAllAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AppBundles
            .Where(b => b.TenantId == tenantId)
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(AppBundle bundle, CancellationToken cancellationToken = default)
    {
        await _context.AppBundles.AddAsync(bundle, cancellationToken);
    }

    public Task UpdateAsync(AppBundle bundle, CancellationToken cancellationToken = default)
    {
        _context.AppBundles.Update(bundle);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(BundleId id, CancellationToken cancellationToken = default)
    {
        var bundle = await GetByIdAsync(id, cancellationToken);
        if (bundle != null)
        {
            _context.AppBundles.Remove(bundle);
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
