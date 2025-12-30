using Microsoft.EntityFrameworkCore;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.TelemetryProcessor.Application.Repositories;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IDeviceHealthScoreRepository.
/// Optimized for TimescaleDB time-series queries.
/// </summary>
public class DeviceHealthScoreRepository : IDeviceHealthScoreRepository
{
    private readonly TelemetryDbContext _context;

    public DeviceHealthScoreRepository(TelemetryDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(DeviceHealthScore healthScore, CancellationToken cancellationToken = default)
    {
        await _context.DeviceHealthScores.AddAsync(healthScore, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<DeviceHealthScore> healthScores, CancellationToken cancellationToken = default)
    {
        await _context.DeviceHealthScores.AddRangeAsync(healthScores, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<DeviceHealthScore?> GetLatestByDeviceIdAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DeviceHealthScores
            .Where(h => h.DeviceId == deviceId)
            .OrderByDescending(h => h.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceHealthScore>> GetByDeviceIdAndTimeRangeAsync(
        DeviceId deviceId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        return await _context.DeviceHealthScores
            .Where(h => h.DeviceId == deviceId &&
                        h.Timestamp >= startTime &&
                        h.Timestamp <= endTime)
            .OrderBy(h => h.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceId>> GetUnhealthyDevicesAsync(
        int healthThreshold,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        // Get the latest health score for each device within the time window
        return await _context.DeviceHealthScores
            .Where(h => h.Timestamp >= since && h.TotalScore < healthThreshold)
            .GroupBy(h => h.DeviceId)
            .Select(g => g.OrderByDescending(h => h.Timestamp).First().DeviceId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<double?> GetAverageHealthScoreAsync(
        DeviceId deviceId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        return await _context.DeviceHealthScores
            .Where(h => h.DeviceId == deviceId &&
                        h.Timestamp >= startTime &&
                        h.Timestamp <= endTime)
            .AverageAsync(h => (double?)h.TotalScore, cancellationToken);
    }

    public async Task<HealthScoreDistribution> GetHealthScoreDistributionAsync(
        TenantId? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        // Get the latest health score for each device
        var latestScores = await _context.DeviceHealthScores
            .GroupBy(h => h.DeviceId)
            .Select(g => g.OrderByDescending(h => h.Timestamp).First())
            .ToListAsync(cancellationToken);

        if (!latestScores.Any())
        {
            return new HealthScoreDistribution(0, 0, 0, 0, 0);
        }

        var healthyCount = latestScores.Count(h => h.TotalScore >= 70);
        var degradedCount = latestScores.Count(h => h.TotalScore >= 40 && h.TotalScore < 70);
        var criticalCount = latestScores.Count(h => h.TotalScore < 40);
        var totalCount = latestScores.Count;
        var averageScore = latestScores.Average(h => h.TotalScore);

        return new HealthScoreDistribution(
            healthyCount,
            degradedCount,
            criticalCount,
            totalCount,
            averageScore);
    }

    public async Task<int> DeleteOlderThanAsync(
        DateTimeOffset cutoffDate,
        CancellationToken cancellationToken = default)
    {
        // Note: TimescaleDB retention policy should handle this automatically
        // This method is for manual cleanup if needed
        var deleted = await _context.DeviceHealthScores
            .Where(h => h.Timestamp < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted;
    }
}
