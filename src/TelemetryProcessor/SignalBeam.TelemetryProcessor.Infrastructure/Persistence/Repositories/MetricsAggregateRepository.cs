using Microsoft.EntityFrameworkCore;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.TelemetryProcessor.Application.Repositories;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository for querying aggregated metrics from TimescaleDB continuous aggregates.
/// </summary>
public class MetricsAggregateRepository : IMetricsAggregateRepository
{
    private readonly TelemetryDbContext _context;

    public MetricsAggregateRepository(TelemetryDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets hourly aggregated metrics for a device.
    /// </summary>
    public async Task<IReadOnlyCollection<HourlyMetricsAggregate>> GetHourlyAggregatesAsync(
        DeviceId deviceId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        var query = @"
            SELECT
                device_id,
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

        var results = new List<HourlyMetricsAggregate>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new HourlyMetricsAggregate(
                DeviceId: new DeviceId(reader.GetGuid(0)),
                Bucket: new DateTimeOffset(reader.GetDateTime(1)),
                AvgCpuUsage: Convert.ToDouble(reader.GetDecimal(2)),
                MaxCpuUsage: Convert.ToDouble(reader.GetDecimal(3)),
                MinCpuUsage: Convert.ToDouble(reader.GetDecimal(4)),
                AvgMemoryUsage: Convert.ToDouble(reader.GetDecimal(5)),
                MaxMemoryUsage: Convert.ToDouble(reader.GetDecimal(6)),
                MinMemoryUsage: Convert.ToDouble(reader.GetDecimal(7)),
                AvgDiskUsage: Convert.ToDouble(reader.GetDecimal(8)),
                MaxDiskUsage: Convert.ToDouble(reader.GetDecimal(9)),
                MinDiskUsage: Convert.ToDouble(reader.GetDecimal(10)),
                DataPoints: Convert.ToInt32(reader.GetInt64(11))
            ));
        }

        return results;
    }

    /// <summary>
    /// Gets daily aggregated metrics for a device.
    /// </summary>
    public async Task<IReadOnlyCollection<DailyMetricsAggregate>> GetDailyAggregatesAsync(
        DeviceId deviceId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        var query = @"
            SELECT
                device_id,
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

        var results = new List<DailyMetricsAggregate>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new DailyMetricsAggregate(
                DeviceId: new DeviceId(reader.GetGuid(0)),
                Bucket: new DateTimeOffset(reader.GetDateTime(1)),
                AvgCpuUsage: Convert.ToDouble(reader.GetDecimal(2)),
                MaxCpuUsage: Convert.ToDouble(reader.GetDecimal(3)),
                MinCpuUsage: Convert.ToDouble(reader.GetDecimal(4)),
                AvgMemoryUsage: Convert.ToDouble(reader.GetDecimal(5)),
                MaxMemoryUsage: Convert.ToDouble(reader.GetDecimal(6)),
                MinMemoryUsage: Convert.ToDouble(reader.GetDecimal(7)),
                AvgDiskUsage: Convert.ToDouble(reader.GetDecimal(8)),
                MaxDiskUsage: Convert.ToDouble(reader.GetDecimal(9)),
                MinDiskUsage: Convert.ToDouble(reader.GetDecimal(10)),
                DataPoints: Convert.ToInt32(reader.GetInt64(11))
            ));
        }

        return results;
    }

    /// <summary>
    /// Gets aggregated metrics for multiple devices.
    /// </summary>
    public async Task<IReadOnlyCollection<HourlyMetricsAggregate>> GetHourlyAggregatesForDevicesAsync(
        IEnumerable<DeviceId> deviceIds,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        var deviceIdValues = deviceIds.Select(id => id.Value).ToArray();

        var query = @"
            SELECT
                device_id,
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
                sample_count
            FROM telemetry_processor.device_metrics_hourly
            WHERE device_id = ANY(@p0)
              AND bucket >= @p1
              AND bucket <= @p2
            ORDER BY device_id, bucket DESC";

        var connection = _context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.Add(new Npgsql.NpgsqlParameter("p0", deviceIdValues));
        command.Parameters.Add(new Npgsql.NpgsqlParameter("p1", startTime));
        command.Parameters.Add(new Npgsql.NpgsqlParameter("p2", endTime));

        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        var results = new List<HourlyMetricsAggregate>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new HourlyMetricsAggregate(
                DeviceId: new DeviceId(reader.GetGuid(0)),
                Bucket: new DateTimeOffset(reader.GetDateTime(1)),
                AvgCpuUsage: Convert.ToDouble(reader.GetDecimal(2)),
                MaxCpuUsage: Convert.ToDouble(reader.GetDecimal(3)),
                MinCpuUsage: Convert.ToDouble(reader.GetDecimal(4)),
                AvgMemoryUsage: Convert.ToDouble(reader.GetDecimal(5)),
                MaxMemoryUsage: Convert.ToDouble(reader.GetDecimal(6)),
                MinMemoryUsage: Convert.ToDouble(reader.GetDecimal(7)),
                AvgDiskUsage: Convert.ToDouble(reader.GetDecimal(8)),
                MaxDiskUsage: Convert.ToDouble(reader.GetDecimal(9)),
                MinDiskUsage: Convert.ToDouble(reader.GetDecimal(10)),
                DataPoints: Convert.ToInt32(reader.GetInt64(11))
            ));
        }

        return results;
    }
}
