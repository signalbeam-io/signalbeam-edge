using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SignalBeam.TelemetryProcessor.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SignalBeam.TelemetryProcessor.Tests.Integration;

/// <summary>
/// Azure PostgreSQL Flexible Server ships the Apache-2 TimescaleDB edition, which
/// rejects compression, retention policies, and continuous aggregates (0A000).
/// These tests run the real migrations under that license to prove they degrade
/// gracefully: hypertables apply, and the rollup relations exist as plain views.
/// </summary>
[Trait("Category", "Integration")]
public class MigrationsApacheLicenseTests : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private TelemetryDbContext? _context;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder("timescale/timescaledb:latest-pg16")
            .WithDatabase("telemetry_apache_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithCommand("-c", "timescaledb.license=apache")
            .Build();

        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<TelemetryDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        _context = new TelemetryDbContext(options);
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
        {
            await _context.DisposeAsync();
        }

        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    [Fact]
    public async Task Migrations_UnderApacheLicense_ApplyCleanly_WithPlainViewRollups()
    {
        await _context!.Database.MigrateAsync();

        // Hypertables are an Apache-edition feature and must exist.
        var hypertables = await CountAsync(
            "SELECT COUNT(*) FROM timescaledb_information.hypertables WHERE hypertable_schema = 'telemetry_processor'");
        hypertables.Should().Be(3);

        // The rollup relations must exist as plain views ('v'), not continuous aggregates.
        var views = await CountAsync(@"
            SELECT COUNT(*) FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'telemetry_processor'
              AND c.relname IN ('device_metrics_hourly', 'device_metrics_daily', 'device_heartbeats_hourly')
              AND c.relkind = 'v'");
        views.Should().Be(3);

        // And they must be queryable.
        var rows = await CountAsync("SELECT COUNT(*) FROM telemetry_processor.device_metrics_hourly");
        rows.Should().Be(0);
    }

    private async Task<long> CountAsync(string sql)
    {
        var connection = _context!.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }
}
