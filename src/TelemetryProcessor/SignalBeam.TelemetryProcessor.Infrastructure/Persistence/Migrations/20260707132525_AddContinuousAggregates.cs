using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalBeam.TelemetryProcessor.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TimescaleDB continuous aggregates (hourly/daily rollups) queried by the
    /// metrics and heartbeat repositories for dashboard reads.
    /// </summary>
    public partial class AddContinuousAggregates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Continuous aggregates cannot be created inside a transaction block
            migrationBuilder.Sql(@"
                CREATE MATERIALIZED VIEW telemetry_processor.device_metrics_hourly
                WITH (timescaledb.continuous) AS
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
                GROUP BY bucket, device_id
                WITH NO DATA;
            ", suppressTransaction: true);

            migrationBuilder.Sql(@"
                SELECT add_continuous_aggregate_policy('telemetry_processor.device_metrics_hourly',
                    start_offset => INTERVAL '3 hours',
                    end_offset => INTERVAL '30 minutes',
                    schedule_interval => INTERVAL '30 minutes');
            ", suppressTransaction: true);

            migrationBuilder.Sql(@"
                CREATE MATERIALIZED VIEW telemetry_processor.device_metrics_daily
                WITH (timescaledb.continuous) AS
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
                GROUP BY bucket, device_id
                WITH NO DATA;
            ", suppressTransaction: true);

            migrationBuilder.Sql(@"
                SELECT add_continuous_aggregate_policy('telemetry_processor.device_metrics_daily',
                    start_offset => INTERVAL '7 days',
                    end_offset => INTERVAL '1 hour',
                    schedule_interval => INTERVAL '2 hours');
            ", suppressTransaction: true);

            migrationBuilder.Sql(@"
                CREATE MATERIALIZED VIEW telemetry_processor.device_heartbeats_hourly
                WITH (timescaledb.continuous) AS
                SELECT
                    time_bucket('1 hour', timestamp) AS bucket,
                    device_id,
                    mode() WITHIN GROUP (ORDER BY status) as most_common_status,
                    COUNT(*) as heartbeat_count,
                    COUNT(DISTINCT ip_address) as unique_ip_count
                FROM telemetry_processor.device_heartbeats
                GROUP BY bucket, device_id
                WITH NO DATA;
            ", suppressTransaction: true);

            migrationBuilder.Sql(@"
                SELECT add_continuous_aggregate_policy('telemetry_processor.device_heartbeats_hourly',
                    start_offset => INTERVAL '3 hours',
                    end_offset => INTERVAL '30 minutes',
                    schedule_interval => INTERVAL '30 minutes');
            ", suppressTransaction: true);

            migrationBuilder.Sql(@"
                CREATE INDEX ix_device_metrics_hourly_device_bucket
                ON telemetry_processor.device_metrics_hourly (device_id, bucket DESC);
            ", suppressTransaction: true);

            migrationBuilder.Sql(@"
                CREATE INDEX ix_device_metrics_daily_device_bucket
                ON telemetry_processor.device_metrics_daily (device_id, bucket DESC);
            ", suppressTransaction: true);

            migrationBuilder.Sql(@"
                CREATE INDEX ix_device_heartbeats_hourly_device_bucket
                ON telemetry_processor.device_heartbeats_hourly (device_id, bucket DESC);
            ", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                SELECT remove_continuous_aggregate_policy('telemetry_processor.device_metrics_hourly', if_exists => true);
            ", suppressTransaction: true);

            migrationBuilder.Sql(@"
                SELECT remove_continuous_aggregate_policy('telemetry_processor.device_metrics_daily', if_exists => true);
            ", suppressTransaction: true);

            migrationBuilder.Sql(@"
                SELECT remove_continuous_aggregate_policy('telemetry_processor.device_heartbeats_hourly', if_exists => true);
            ", suppressTransaction: true);

            migrationBuilder.Sql(@"
                DROP MATERIALIZED VIEW IF EXISTS telemetry_processor.device_heartbeats_hourly CASCADE;
            ", suppressTransaction: true);

            migrationBuilder.Sql(@"
                DROP MATERIALIZED VIEW IF EXISTS telemetry_processor.device_metrics_daily CASCADE;
            ", suppressTransaction: true);

            migrationBuilder.Sql(@"
                DROP MATERIALIZED VIEW IF EXISTS telemetry_processor.device_metrics_hourly CASCADE;
            ", suppressTransaction: true);
        }
    }
}
