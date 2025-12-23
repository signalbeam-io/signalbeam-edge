using Microsoft.EntityFrameworkCore;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IRolloutStatusRepository.
/// </summary>
public class RolloutStatusRepository : IRolloutStatusRepository
{
    private readonly BundleDbContext _context;

    public RolloutStatusRepository(BundleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<RolloutStatus?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.RolloutStatuses
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<RolloutStatus?> GetByDeviceAndBundleAsync(
        DeviceId deviceId,
        BundleId bundleId,
        BundleVersion version,
        CancellationToken cancellationToken = default)
    {
        return await _context.RolloutStatuses
            .FirstOrDefaultAsync(
                r => r.DeviceId == deviceId && r.BundleId == bundleId && r.BundleVersion == version,
                cancellationToken);
    }

    public async Task<IReadOnlyList<RolloutStatus>> GetByBundleAsync(
        BundleId bundleId,
        CancellationToken cancellationToken = default)
    {
        return await _context.RolloutStatuses
            .Where(r => r.BundleId == bundleId)
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RolloutStatus>> GetByDeviceAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
    {
        return await _context.RolloutStatuses
            .Where(r => r.DeviceId == deviceId)
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RolloutStatus>> GetByRolloutIdAsync(
        Guid rolloutId,
        CancellationToken cancellationToken = default)
    {
        return await _context.RolloutStatuses
            .Where(r => r.RolloutId == rolloutId)
            .OrderBy(r => r.DeviceId)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Guid> RolloutIds, int TotalCount)> GetDistinctRolloutsAsync(
        BundleId? bundleId = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _context.RolloutStatuses.AsQueryable();

        // Filter by bundle ID if provided
        if (bundleId is not null)
        {
            query = query.Where(r => r.BundleId == bundleId);
        }

        // Get distinct rollout IDs ordered by creation date (using StartedAt as proxy)
        var distinctRolloutIds = await query
            .GroupBy(r => r.RolloutId)
            .Select(g => new { RolloutId = g.Key, StartedAt = g.Min(r => r.StartedAt) })
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync(cancellationToken);

        var totalCount = distinctRolloutIds.Count;

        // Apply pagination
        var paginatedRolloutIds = distinctRolloutIds
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => r.RolloutId)
            .ToList();

        return (paginatedRolloutIds, totalCount);
    }

    public async Task AddAsync(RolloutStatus rolloutStatus, CancellationToken cancellationToken = default)
    {
        await _context.RolloutStatuses.AddAsync(rolloutStatus, cancellationToken);
    }

    public Task UpdateAsync(RolloutStatus rolloutStatus, CancellationToken cancellationToken = default)
    {
        _context.RolloutStatuses.Update(rolloutStatus);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
