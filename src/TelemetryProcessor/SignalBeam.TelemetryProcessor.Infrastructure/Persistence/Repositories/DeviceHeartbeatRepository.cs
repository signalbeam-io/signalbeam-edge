using Microsoft.EntityFrameworkCore;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository for DeviceHeartbeat with optimized time-series queries.
/// </summary>
public class DeviceHeartbeatRepository
{
    private readonly TelemetryDbContext _context;

    public DeviceHeartbeatRepository(TelemetryDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Adds a new device heartbeat record.
    /// </summary>
    public async Task AddAsync(DeviceHeartbeat heartbeat, CancellationToken cancellationToken = default)
    {
        await _context.DeviceHeartbeats.AddAsync(heartbeat, cancellationToken);
    }

    /// <summary>
    /// Adds multiple device heartbeat records in batch.
    /// Optimized for bulk inserts.
    /// </summary>
    public async Task AddRangeAsync(IEnumerable<DeviceHeartbeat> heartbeats, CancellationToken cancellationToken = default)
    {
        await _context.DeviceHeartbeats.AddRangeAsync(heartbeats, cancellationToken);
    }

    /// <summary>
    /// Gets device heartbeats for a specific device within a time range.
    /// Optimized query using TimescaleDB time-based partitioning.
    /// </summary>
    public async Task<List<DeviceHeartbeat>> GetByDeviceAndTimeRangeAsync(
        DeviceId deviceId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        return await _context.DeviceHeartbeats
            .Where(h => h.DeviceId == deviceId
                && h.Timestamp >= startTime
                && h.Timestamp <= endTime)
            .OrderByDescending(h => h.Timestamp)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the latest heartbeat for a specific device.
    /// Used to determine device online/offline status.
    /// </summary>
    public async Task<DeviceHeartbeat?> GetLatestByDeviceAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DeviceHeartbeats
            .Where(h => h.DeviceId == deviceId)
            .OrderByDescending(h => h.Timestamp)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the latest heartbeats for multiple devices.
    /// Optimized for dashboard queries showing device status.
    /// </summary>
    public async Task<Dictionary<Guid, DeviceHeartbeat>> GetLatestByDevicesAsync(
        IEnumerable<DeviceId> deviceIds,
        CancellationToken cancellationToken = default)
    {
        var deviceIdValues = deviceIds.Select(id => id.Value).ToList();

        // Use window function to get the latest heartbeat per device
        var query = @"
            WITH ranked_heartbeats AS (
                SELECT
                    id,
                    timestamp,
                    device_id,
                    status,
                    ip_address,
                    additional_data,
                    ROW_NUMBER() OVER (PARTITION BY device_id ORDER BY timestamp DESC) as rn
                FROM telemetry_processor.device_heartbeats
                WHERE device_id = ANY(@p0)
            )
            SELECT
                id,
                timestamp,
                device_id,
                status,
                ip_address,
                additional_data
            FROM ranked_heartbeats
            WHERE rn = 1";

        var connection = _context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.Add(new Npgsql.NpgsqlParameter("p0", deviceIdValues.ToArray()));

        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        var results = new Dictionary<Guid, DeviceHeartbeat>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var deviceId = reader.GetGuid(2);
            var heartbeat = DeviceHeartbeat.Create(
                new DeviceId(deviceId),
                new DateTimeOffset(reader.GetDateTime(1)),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)
            );

            results[deviceId] = heartbeat;
        }

        return results;
    }

    /// <summary>
    /// Gets hourly aggregated heartbeat statistics for a device.
    /// Uses TimescaleDB continuous aggregate for optimal performance.
    /// </summary>
    public async Task<List<HourlyHeartbeatStats>> GetHourlyStatsAsync(
        DeviceId deviceId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        var query = @"
            SELECT
                bucket,
                most_common_status,
                heartbeat_count,
                unique_ip_count
            FROM telemetry_processor.device_heartbeats_hourly
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

        var results = new List<HourlyHeartbeatStats>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new HourlyHeartbeatStats
            {
                Bucket = reader.GetDateTime(0),
                MostCommonStatus = reader.GetString(1),
                HeartbeatCount = reader.GetInt64(2),
                UniqueIpCount = reader.GetInt64(3)
            });
        }

        return results;
    }

    /// <summary>
    /// Counts heartbeats for a device within a time range.
    /// Useful for uptime calculations.
    /// </summary>
    public async Task<int> CountByDeviceAndTimeRangeAsync(
        DeviceId deviceId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        return await _context.DeviceHeartbeats
            .Where(h => h.DeviceId == deviceId
                && h.Timestamp >= startTime
                && h.Timestamp <= endTime)
            .CountAsync(cancellationToken);
    }

    /// <summary>
    /// Gets devices that haven't sent a heartbeat within the specified time window.
    /// Useful for detecting offline devices.
    /// </summary>
    public async Task<List<Guid>> GetInactiveDevicesAsync(
        TimeSpan inactivityThreshold,
        CancellationToken cancellationToken = default)
    {
        var cutoffTime = DateTimeOffset.UtcNow - inactivityThreshold;

        // Get all distinct device IDs
        var allDevices = await _context.DeviceHeartbeats
            .Select(h => h.DeviceId.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Get devices with recent heartbeats
        var activeDevices = await _context.DeviceHeartbeats
            .Where(h => h.Timestamp >= cutoffTime)
            .Select(h => h.DeviceId.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Return devices without recent heartbeats
        return allDevices.Except(activeDevices).ToList();
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
/// Represents hourly heartbeat statistics from the continuous aggregate view.
/// </summary>
public class HourlyHeartbeatStats
{
    public DateTime Bucket { get; set; }
    public string MostCommonStatus { get; set; } = string.Empty;
    public long HeartbeatCount { get; set; }
    public long UniqueIpCount { get; set; }
}
