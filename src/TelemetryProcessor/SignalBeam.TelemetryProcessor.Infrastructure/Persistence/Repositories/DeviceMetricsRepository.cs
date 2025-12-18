using Microsoft.EntityFrameworkCore;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository for DeviceMetrics with optimized time-series queries.
/// </summary>
public class DeviceMetricsRepository
{
    private readonly TelemetryDbContext _context;

    public DeviceMetricsRepository(TelemetryDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Adds a new device metrics record.
    /// </summary>
    public async Task AddAsync(DeviceMetrics metrics, CancellationToken cancellationToken = default)
    {
        await _context.DeviceMetrics.AddAsync(metrics, cancellationToken);
    }

    /// <summary>
    /// Adds multiple device metrics records in batch.
    /// Optimized for bulk inserts.
    /// </summary>
    public async Task AddRangeAsync(IEnumerable<DeviceMetrics> metrics, CancellationToken cancellationToken = default)
    {
        await _context.DeviceMetrics.AddRangeAsync(metrics, cancellationToken);
    }

    /// <summary>
    /// Gets device metrics for a specific device within a time range.
    /// Optimized query using TimescaleDB time-based partitioning.
    /// </summary>
    public async Task<List<DeviceMetrics>> GetByDeviceAndTimeRangeAsync(
        DeviceId deviceId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        return await _context.DeviceMetrics
            .Where(m => m.DeviceId == deviceId
                && m.Timestamp >= startTime
                && m.Timestamp <= endTime)
            .OrderByDescending(m => m.Timestamp)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the latest metrics for a specific device.
    /// </summary>
    public async Task<DeviceMetrics?> GetLatestByDeviceAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DeviceMetrics
            .Where(m => m.DeviceId == deviceId)
            .OrderByDescending(m => m.Timestamp)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Gets hourly aggregated metrics for a device within a time range.
    /// Uses TimescaleDB continuous aggregate for optimal performance.
    /// </summary>
    public async Task<List<HourlyMetrics>> GetHourlyAggregatesAsync(
        DeviceId deviceId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        // Query the continuous aggregate materialized view
        var query = @"
            SELECT
                bucket,
                avg_cpu_usage,
                max_cpu_usage,
                min_cpu_usage,
                avg_memory_usage,
                max_memory_usage,
                min_memory_usage,
                avg_disk_usage,
                max_disk_usage,
                min_disk_usage,
                avg_uptime_seconds,
                avg_running_containers,
                sample_count
            FROM telemetry_processor.device_metrics_hourly
            WHERE device_id = @p0
              AND bucket >= @p1
              AND bucket <= @p2
            ORDER BY bucket DESC";

        var connection = _context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.Add(new Npgsql.NpgsqlParameter("p0", deviceId.Value));
        command.Parameters.Add(new Npgsql.NpgsqlParameter("p1", startTime));
        command.Parameters.Add(new Npgsql.NpgsqlParameter("p2", endTime));

        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        var results = new List<HourlyMetrics>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new HourlyMetrics
            {
                Bucket = reader.GetDateTime(0),
                AvgCpuUsage = reader.GetDecimal(1),
                MaxCpuUsage = reader.GetDecimal(2),
                MinCpuUsage = reader.GetDecimal(3),
                AvgMemoryUsage = reader.GetDecimal(4),
                MaxMemoryUsage = reader.GetDecimal(5),
                MinMemoryUsage = reader.GetDecimal(6),
                AvgDiskUsage = reader.GetDecimal(7),
                MaxDiskUsage = reader.GetDecimal(8),
                MinDiskUsage = reader.GetDecimal(9),
                AvgUptimeSeconds = reader.GetDouble(10),
                AvgRunningContainers = reader.GetDouble(11),
                SampleCount = reader.GetInt64(12)
            });
        }

        return results;
    }

    /// <summary>
    /// Gets daily aggregated metrics for a device within a time range.
    /// Uses TimescaleDB continuous aggregate for optimal performance.
    /// </summary>
    public async Task<List<DailyMetrics>> GetDailyAggregatesAsync(
        DeviceId deviceId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        var query = @"
            SELECT
                bucket,
                avg_cpu_usage,
                max_cpu_usage,
                min_cpu_usage,
                avg_memory_usage,
                max_memory_usage,
                min_memory_usage,
                avg_disk_usage,
                max_disk_usage,
                min_disk_usage,
                avg_uptime_seconds,
                avg_running_containers,
                sample_count
            FROM telemetry_processor.device_metrics_daily
            WHERE device_id = @p0
              AND bucket >= @p1
              AND bucket <= @p2
            ORDER BY bucket DESC";

        var connection = _context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.Add(new Npgsql.NpgsqlParameter("p0", deviceId.Value));
        command.Parameters.Add(new Npgsql.NpgsqlParameter("p1", startTime));
        command.Parameters.Add(new Npgsql.NpgsqlParameter("p2", endTime));

        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        var results = new List<DailyMetrics>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new DailyMetrics
            {
                Bucket = reader.GetDateTime(0),
                AvgCpuUsage = reader.GetDecimal(1),
                MaxCpuUsage = reader.GetDecimal(2),
                MinCpuUsage = reader.GetDecimal(3),
                AvgMemoryUsage = reader.GetDecimal(4),
                MaxMemoryUsage = reader.GetDecimal(5),
                MinMemoryUsage = reader.GetDecimal(6),
                AvgDiskUsage = reader.GetDecimal(7),
                MaxDiskUsage = reader.GetDecimal(8),
                MinDiskUsage = reader.GetDecimal(9),
                AvgUptimeSeconds = reader.GetDouble(10),
                AvgRunningContainers = reader.GetDouble(11),
                SampleCount = reader.GetInt64(12)
            });
        }

        return results;
    }

    /// <summary>
    /// Saves all pending changes to the database.
    /// </summary>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Represents hourly aggregated metrics from the continuous aggregate view.
/// </summary>
public class HourlyMetrics
{
    public DateTime Bucket { get; set; }
    public decimal AvgCpuUsage { get; set; }
    public decimal MaxCpuUsage { get; set; }
    public decimal MinCpuUsage { get; set; }
    public decimal AvgMemoryUsage { get; set; }
    public decimal MaxMemoryUsage { get; set; }
    public decimal MinMemoryUsage { get; set; }
    public decimal AvgDiskUsage { get; set; }
    public decimal MaxDiskUsage { get; set; }
    public decimal MinDiskUsage { get; set; }
    public double AvgUptimeSeconds { get; set; }
    public double AvgRunningContainers { get; set; }
    public long SampleCount { get; set; }
}

/// <summary>
/// Represents daily aggregated metrics from the continuous aggregate view.
/// </summary>
public class DailyMetrics
{
    public DateTime Bucket { get; set; }
    public decimal AvgCpuUsage { get; set; }
    public decimal MaxCpuUsage { get; set; }
    public decimal MinCpuUsage { get; set; }
    public decimal AvgMemoryUsage { get; set; }
    public decimal MaxMemoryUsage { get; set; }
    public decimal MinMemoryUsage { get; set; }
    public decimal AvgDiskUsage { get; set; }
    public decimal MaxDiskUsage { get; set; }
    public decimal MinDiskUsage { get; set; }
    public double AvgUptimeSeconds { get; set; }
    public double AvgRunningContainers { get; set; }
    public long SampleCount { get; set; }
}
