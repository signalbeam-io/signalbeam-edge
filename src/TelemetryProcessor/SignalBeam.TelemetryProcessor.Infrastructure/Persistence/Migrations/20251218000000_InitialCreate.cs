using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalBeam.TelemetryProcessor.Infrastructure.Persistence.Migrations;

/// <summary>
/// Initial migration for TelemetryProcessor service.
/// Creates TimescaleDB hypertables for device metrics and heartbeats.
/// </summary>
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Ensure TimescaleDB extension is enabled
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS timescaledb CASCADE;");

        // Create schema
        migrationBuilder.EnsureSchema(name: "telemetry_processor");

        // Create device_heartbeats table
        migrationBuilder.CreateTable(
            name: "device_heartbeats",
            schema: "telemetry_processor",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                device_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                additional_data = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_device_heartbeats", x => new { x.id, x.timestamp });
            },
            comment: "TimescaleDB hypertable for device heartbeats");

        // Create device_metrics table
        migrationBuilder.CreateTable(
            name: "device_metrics",
            schema: "telemetry_processor",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                device_id = table.Column<Guid>(type: "uuid", nullable: false),
                cpu_usage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                memory_usage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                disk_usage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                uptime_seconds = table.Column<long>(type: "bigint", nullable: false),
                running_containers = table.Column<int>(type: "integer", nullable: false),
                additional_metrics = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_device_metrics", x => new { x.id, x.timestamp });
            },
            comment: "TimescaleDB hypertable for device metrics");

        // Convert to TimescaleDB hypertables
        migrationBuilder.Sql(@"
            SELECT create_hypertable('telemetry_processor.device_heartbeats', 'timestamp',
                chunk_time_interval => INTERVAL '1 day',
                if_not_exists => TRUE);
        ");

        migrationBuilder.Sql(@"
            SELECT create_hypertable('telemetry_processor.device_metrics', 'timestamp',
                chunk_time_interval => INTERVAL '1 day',
                if_not_exists => TRUE);
        ");

        // Create indexes for device_heartbeats
        migrationBuilder.CreateIndex(
            name: "ix_device_heartbeats_device_id",
            schema: "telemetry_processor",
            table: "device_heartbeats",
            column: "device_id");

        migrationBuilder.CreateIndex(
            name: "ix_device_heartbeats_timestamp",
            schema: "telemetry_processor",
            table: "device_heartbeats",
            column: "timestamp",
            descending: new[] { true });

        migrationBuilder.CreateIndex(
            name: "ix_device_heartbeats_device_timestamp",
            schema: "telemetry_processor",
            table: "device_heartbeats",
            columns: new[] { "device_id", "timestamp" },
            descending: new[] { false, true });

        // Create indexes for device_metrics
        migrationBuilder.CreateIndex(
            name: "ix_device_metrics_device_id",
            schema: "telemetry_processor",
            table: "device_metrics",
            column: "device_id");

        migrationBuilder.CreateIndex(
            name: "ix_device_metrics_timestamp",
            schema: "telemetry_processor",
            table: "device_metrics",
            column: "timestamp",
            descending: new[] { true });

        migrationBuilder.CreateIndex(
            name: "ix_device_metrics_device_timestamp",
            schema: "telemetry_processor",
            table: "device_metrics",
            columns: new[] { "device_id", "timestamp" },
            descending: new[] { false, true });

        // Configure compression for device_heartbeats (compress data older than 7 days)
        migrationBuilder.Sql(@"
            ALTER TABLE telemetry_processor.device_heartbeats SET (
                timescaledb.compress,
                timescaledb.compress_segmentby = 'device_id'
            );
        ");

        migrationBuilder.Sql(@"
            SELECT add_compression_policy('telemetry_processor.device_heartbeats', INTERVAL '7 days');
        ");

        // Configure compression for device_metrics (compress data older than 7 days)
        migrationBuilder.Sql(@"
            ALTER TABLE telemetry_processor.device_metrics SET (
                timescaledb.compress,
                timescaledb.compress_segmentby = 'device_id',
                timescaledb.compress_orderby = 'timestamp DESC'
            );
        ");

        migrationBuilder.Sql(@"
            SELECT add_compression_policy('telemetry_processor.device_metrics', INTERVAL '7 days');
        ");

        // Configure retention policies (drop data older than 90 days)
        migrationBuilder.Sql(@"
            SELECT add_retention_policy('telemetry_processor.device_heartbeats', INTERVAL '90 days');
        ");

        migrationBuilder.Sql(@"
            SELECT add_retention_policy('telemetry_processor.device_metrics', INTERVAL '90 days');
        ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Drop compression and retention policies
        migrationBuilder.Sql(@"
            SELECT remove_retention_policy('telemetry_processor.device_heartbeats', if_exists => true);
        ");

        migrationBuilder.Sql(@"
            SELECT remove_retention_policy('telemetry_processor.device_metrics', if_exists => true);
        ");

        migrationBuilder.Sql(@"
            SELECT remove_compression_policy('telemetry_processor.device_heartbeats', if_exists => true);
        ");

        migrationBuilder.Sql(@"
            SELECT remove_compression_policy('telemetry_processor.device_metrics', if_exists => true);
        ");

        // Drop tables (this will also remove hypertables)
        migrationBuilder.DropTable(
            name: "device_heartbeats",
            schema: "telemetry_processor");

        migrationBuilder.DropTable(
            name: "device_metrics",
            schema: "telemetry_processor");

        // Drop schema
        migrationBuilder.Sql("DROP SCHEMA IF EXISTS telemetry_processor CASCADE;");
    }
}
