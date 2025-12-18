using Microsoft.EntityFrameworkCore;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IBundleVersionRepository.
/// </summary>
public class BundleVersionRepository : IBundleVersionRepository
{
    private readonly BundleDbContext _context;

    public BundleVersionRepository(BundleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<AppBundleVersion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.AppBundleVersions
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<AppBundleVersion?> GetByBundleAndVersionAsync(
        BundleId bundleId,
        BundleVersion version,
        CancellationToken cancellationToken = default)
    {
        return await _context.AppBundleVersions
            .FirstOrDefaultAsync(
                v => v.BundleId == bundleId && v.Version == version,
                cancellationToken);
    }

    public async Task<IReadOnlyList<AppBundleVersion>> GetAllVersionsAsync(
        BundleId bundleId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AppBundleVersions
            .Where(v => v.BundleId == bundleId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(AppBundleVersion bundleVersion, CancellationToken cancellationToken = default)
    {
        await _context.AppBundleVersions.AddAsync(bundleVersion, cancellationToken);
    }

    public Task UpdateAsync(AppBundleVersion bundleVersion, CancellationToken cancellationToken = default)
    {
        _context.AppBundleVersions.Update(bundleVersion);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
