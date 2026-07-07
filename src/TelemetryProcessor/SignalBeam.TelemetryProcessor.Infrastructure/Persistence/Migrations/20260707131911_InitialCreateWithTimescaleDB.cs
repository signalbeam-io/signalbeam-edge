using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalBeam.TelemetryProcessor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateWithTimescaleDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CREATE EXTENSION timescaledb is not allowed inside a transaction block
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS timescaledb CASCADE;", suppressTransaction: true);

            migrationBuilder.EnsureSchema(
                name: "telemetry_processor");

            migrationBuilder.CreateTable(
                name: "alerts",
                schema: "telemetry_processor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rollout_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    acknowledged_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerts", x => x.id);
                },
                comment: "System alerts for monitoring and notifications");

            migrationBuilder.CreateTable(
                name: "device_health_scores",
                schema: "telemetry_processor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_score = table.Column<int>(type: "integer", nullable: false),
                    heartbeat_score = table.Column<int>(type: "integer", nullable: false),
                    reconciliation_score = table.Column<int>(type: "integer", nullable: false),
                    resource_score = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_health_scores", x => new { x.id, x.timestamp });
                },
                comment: "TimescaleDB hypertable for device health scores");

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

            migrationBuilder.CreateTable(
                name: "device_metrics",
                schema: "telemetry_processor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cpu_usage = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false),
                    memory_usage = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false),
                    disk_usage = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false),
                    uptime_seconds = table.Column<long>(type: "bigint", nullable: false),
                    running_containers = table.Column<int>(type: "integer", nullable: false),
                    additional_metrics = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_metrics", x => new { x.id, x.timestamp });
                },
                comment: "TimescaleDB hypertable for device metrics");

            migrationBuilder.CreateTable(
                name: "alert_notifications",
                schema: "telemetry_processor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    alert_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    recipient = table.Column<string>(type: "text", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_alert_notifications_alerts_alert_id",
                        column: x => x.alert_id,
                        principalSchema: "telemetry_processor",
                        principalTable: "alerts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Alert notification delivery records");

            migrationBuilder.CreateIndex(
                name: "ix_alert_notifications_alert_id",
                schema: "telemetry_processor",
                table: "alert_notifications",
                column: "alert_id");

            migrationBuilder.CreateIndex(
                name: "ix_alert_notifications_sent_at",
                schema: "telemetry_processor",
                table: "alert_notifications",
                column: "sent_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_alert_notifications_success",
                schema: "telemetry_processor",
                table: "alert_notifications",
                column: "success");

            migrationBuilder.CreateIndex(
                name: "ix_alerts_created_at",
                schema: "telemetry_processor",
                table: "alerts",
                column: "created_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_alerts_device_id",
                schema: "telemetry_processor",
                table: "alerts",
                column: "device_id",
                filter: "device_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_alerts_device_type_status",
                schema: "telemetry_processor",
                table: "alerts",
                columns: new[] { "device_id", "type", "status" },
                filter: "device_id IS NOT NULL AND status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_alerts_status",
                schema: "telemetry_processor",
                table: "alerts",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_alerts_tenant_id",
                schema: "telemetry_processor",
                table: "alerts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_alerts_type_severity",
                schema: "telemetry_processor",
                table: "alerts",
                columns: new[] { "type", "severity" });

            migrationBuilder.CreateIndex(
                name: "ix_device_health_scores_device_id",
                schema: "telemetry_processor",
                table: "device_health_scores",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_health_scores_device_timestamp",
                schema: "telemetry_processor",
                table: "device_health_scores",
                columns: new[] { "device_id", "timestamp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_device_health_scores_timestamp",
                schema: "telemetry_processor",
                table: "device_health_scores",
                column: "timestamp",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_device_health_scores_total_score",
                schema: "telemetry_processor",
                table: "device_health_scores",
                column: "total_score");

            migrationBuilder.CreateIndex(
                name: "ix_device_heartbeats_device_id",
                schema: "telemetry_processor",
                table: "device_heartbeats",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_heartbeats_device_timestamp",
                schema: "telemetry_processor",
                table: "device_heartbeats",
                columns: new[] { "device_id", "timestamp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_device_heartbeats_timestamp",
                schema: "telemetry_processor",
                table: "device_heartbeats",
                column: "timestamp",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_device_metrics_device_id",
                schema: "telemetry_processor",
                table: "device_metrics",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_metrics_device_timestamp",
                schema: "telemetry_processor",
                table: "device_metrics",
                columns: new[] { "device_id", "timestamp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_device_metrics_timestamp",
                schema: "telemetry_processor",
                table: "device_metrics",
                column: "timestamp",
                descending: new bool[0]);

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

            migrationBuilder.Sql(@"
                SELECT create_hypertable('telemetry_processor.device_health_scores', 'timestamp',
                    chunk_time_interval => INTERVAL '1 day',
                    if_not_exists => TRUE);
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE telemetry_processor.device_heartbeats SET (
                    timescaledb.compress,
                    timescaledb.compress_segmentby = 'device_id',
                    timescaledb.compress_orderby = 'timestamp DESC'
                );
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE telemetry_processor.device_metrics SET (
                    timescaledb.compress,
                    timescaledb.compress_segmentby = 'device_id',
                    timescaledb.compress_orderby = 'timestamp DESC'
                );
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE telemetry_processor.device_health_scores SET (
                    timescaledb.compress,
                    timescaledb.compress_segmentby = 'device_id',
                    timescaledb.compress_orderby = 'timestamp DESC'
                );
            ");

            migrationBuilder.Sql(@"
                SELECT add_compression_policy('telemetry_processor.device_heartbeats', INTERVAL '7 days', if_not_exists => TRUE);
            ");

            migrationBuilder.Sql(@"
                SELECT add_compression_policy('telemetry_processor.device_metrics', INTERVAL '7 days', if_not_exists => TRUE);
            ");

            migrationBuilder.Sql(@"
                SELECT add_compression_policy('telemetry_processor.device_health_scores', INTERVAL '7 days', if_not_exists => TRUE);
            ");

            migrationBuilder.Sql(@"
                SELECT add_retention_policy('telemetry_processor.device_heartbeats', INTERVAL '90 days', if_not_exists => TRUE);
            ");

            migrationBuilder.Sql(@"
                SELECT add_retention_policy('telemetry_processor.device_metrics', INTERVAL '90 days', if_not_exists => TRUE);
            ");

            migrationBuilder.Sql(@"
                SELECT add_retention_policy('telemetry_processor.device_health_scores', INTERVAL '90 days', if_not_exists => TRUE);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                SELECT remove_retention_policy('telemetry_processor.device_heartbeats', if_exists => true);
                SELECT remove_retention_policy('telemetry_processor.device_metrics', if_exists => true);
                SELECT remove_retention_policy('telemetry_processor.device_health_scores', if_exists => true);
                SELECT remove_compression_policy('telemetry_processor.device_heartbeats', if_exists => true);
                SELECT remove_compression_policy('telemetry_processor.device_metrics', if_exists => true);
                SELECT remove_compression_policy('telemetry_processor.device_health_scores', if_exists => true);
            ");

            migrationBuilder.DropTable(
                name: "alert_notifications",
                schema: "telemetry_processor");

            migrationBuilder.DropTable(
                name: "device_health_scores",
                schema: "telemetry_processor");

            migrationBuilder.DropTable(
                name: "device_heartbeats",
                schema: "telemetry_processor");

            migrationBuilder.DropTable(
                name: "device_metrics",
                schema: "telemetry_processor");

            migrationBuilder.DropTable(
                name: "alerts",
                schema: "telemetry_processor");
        }
    }
}
