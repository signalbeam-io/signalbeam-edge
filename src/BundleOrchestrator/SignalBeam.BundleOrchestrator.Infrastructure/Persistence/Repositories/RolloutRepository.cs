using Microsoft.EntityFrameworkCore;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IRolloutRepository.
/// </summary>
public class RolloutRepository : IRolloutRepository
{
    private readonly BundleDbContext _context;

    public RolloutRepository(BundleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Rollout?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Rollouts
            .Include(r => r.Phases.OrderBy(p => p.PhaseNumber)) // Rollout indexes phases by position
                .ThenInclude(p => p.DeviceAssignments)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<Rollout?> GetByIdWithoutIncludesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Rollouts
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Rollout>> GetAllAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Rollouts
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Rollout> Rollouts, int TotalCount)> GetPagedAsync(
        TenantId tenantId,
        RolloutLifecycleStatus? status = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Rollouts
            .Where(r => r.TenantId == tenantId);

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rollouts = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (rollouts, totalCount);
    }

    public async Task<IReadOnlyList<Rollout>> GetActiveRolloutsAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Rollouts
            .Include(r => r.Phases.OrderBy(p => p.PhaseNumber)) // Rollout indexes phases by position
                .ThenInclude(p => p.DeviceAssignments)
            .Where(r => r.TenantId == tenantId &&
                       (r.Status == RolloutLifecycleStatus.InProgress ||
                        r.Status == RolloutLifecycleStatus.Paused))
            .OrderBy(r => r.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Rollout>> GetByBundleIdAsync(
        BundleId bundleId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Rollouts
            .Where(r => r.BundleId == bundleId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Rollout>> GetByDeviceIdAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
    {
        // Find rollouts that have assignments for this device
        return await _context.Rollouts
            .Include(r => r.Phases.OrderBy(p => p.PhaseNumber)) // Rollout indexes phases by position
                .ThenInclude(p => p.DeviceAssignments)
            .Where(r => r.Phases.Any(p =>
                p.DeviceAssignments.Any(a => a.DeviceId == deviceId)))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasActiveRolloutAsync(
        BundleId bundleId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Rollouts
            .AnyAsync(r => r.BundleId == bundleId &&
                          (r.Status == RolloutLifecycleStatus.InProgress ||
                           r.Status == RolloutLifecycleStatus.Paused),
                     cancellationToken);
    }

    public async Task AddAsync(Rollout rollout, CancellationToken cancellationToken = default)
    {
        await _context.Rollouts.AddAsync(rollout, cancellationToken);
    }

    public Task UpdateAsync(Rollout rollout, CancellationToken cancellationToken = default)
    {
        // Device assignments get their IDs in the domain, so Update() or change detection would take
        // a new one on a loaded rollout for an existing row and UPDATE nothing. Track new ones as
        // Added before change detection can see them.
        var autoDetectChanges = _context.ChangeTracker.AutoDetectChangesEnabled;
        _context.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            if (_context.Entry(rollout).State == EntityState.Detached)
            {
                _context.Rollouts.Update(rollout);
                return Task.CompletedTask;
            }

            foreach (var assignment in rollout.Phases.SelectMany(p => p.DeviceAssignments))
            {
                if (_context.Entry(assignment).State == EntityState.Detached)
                {
                    _context.Add(assignment);
                }
            }
        }
        finally
        {
            _context.ChangeTracker.AutoDetectChangesEnabled = autoDetectChanges;
        }

        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rollout = await GetByIdWithoutIncludesAsync(id, cancellationToken);
        if (rollout != null)
        {
            _context.Rollouts.Remove(rollout);
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
