using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalBeam.TelemetryProcessor.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Hourly/daily rollup views queried by the metrics and heartbeat repositories.
    /// Plain SQL views rather than TimescaleDB continuous aggregates: Azure
    /// PostgreSQL ships the Apache-2 edition, which rejects continuous aggregates
    /// (0A000) at parse time — even behind IF NOT EXISTS — so a single portable
    /// definition is used on every edition. Revisit materialized rollups when
    /// query volume warrants it.
    /// </summary>
    public partial class AddContinuousAggregates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE VIEW telemetry_processor.device_metrics_hourly AS
                SELECT
                    time_bucket('1 hour', timestamp) AS bucket,
                    device_id,
                    AVG(cpu_usage) as avg_cpu_usage,
                    MAX(cpu_usage) as max_cpu_usage,
                    MIN(cpu_usage) as min_cpu_usage,
                    AVG(memory_usage) as avg_memory_usage,
                    MAX(memory_usage) as max_memory_usage,
                    MIN(memory_usage) as min_memory_usage,
                    AVG(disk_usage) as avg_disk_usage,
                    MAX(disk_usage) as max_disk_usage,
                    MIN(disk_usage) as min_disk_usage,
                    AVG(uptime_seconds) as avg_uptime_seconds,
                    AVG(running_containers) as avg_running_containers,
                    COUNT(*) as sample_count
                FROM telemetry_processor.device_metrics
                GROUP BY bucket, device_id;
            ");

            migrationBuilder.Sql(@"
                CREATE VIEW telemetry_processor.device_metrics_daily AS
                SELECT
                    time_bucket('1 day', timestamp) AS bucket,
                    device_id,
                    AVG(cpu_usage) as avg_cpu_usage,
                    MAX(cpu_usage) as max_cpu_usage,
                    MIN(cpu_usage) as min_cpu_usage,
                    AVG(memory_usage) as avg_memory_usage,
                    MAX(memory_usage) as max_memory_usage,
                    MIN(memory_usage) as min_memory_usage,
                    AVG(disk_usage) as avg_disk_usage,
                    MAX(disk_usage) as max_disk_usage,
                    MIN(disk_usage) as min_disk_usage,
                    AVG(uptime_seconds) as avg_uptime_seconds,
                    AVG(running_containers) as avg_running_containers,
                    COUNT(*) as sample_count
                FROM telemetry_processor.device_metrics
                GROUP BY bucket, device_id;
            ");

            migrationBuilder.Sql(@"
                CREATE VIEW telemetry_processor.device_heartbeats_hourly AS
                SELECT
                    time_bucket('1 hour', timestamp) AS bucket,
                    device_id,
                    mode() WITHIN GROUP (ORDER BY status) as most_common_status,
                    COUNT(*) as heartbeat_count,
                    COUNT(DISTINCT ip_address) as unique_ip_count
                FROM telemetry_processor.device_heartbeats
                GROUP BY bucket, device_id;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS telemetry_processor.device_heartbeats_hourly CASCADE;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS telemetry_processor.device_metrics_daily CASCADE;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS telemetry_processor.device_metrics_hourly CASCADE;");
        }
    }
}
