using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalBeam.TelemetryProcessor.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddMetricsAndAlerting : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // =====================================================
        // DeviceHealthScores Table
        // =====================================================
        migrationBuilder.CreateTable(
            name: "device_health_scores",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                device_id = table.Column<Guid>(type: "uuid", nullable: false),
                total_score = table.Column<int>(type: "integer", nullable: false),
                heartbeat_score = table.Column<int>(type: "integer", nullable: false),
                reconciliation_score = table.Column<int>(type: "integer", nullable: false),
                resource_score = table.Column<int>(type: "integer", nullable: false),
                timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_device_health_scores", x => x.id);
                table.CheckConstraint("ck_device_health_scores_total_score", "total_score >= 0 AND total_score <= 100");
                table.CheckConstraint("ck_device_health_scores_heartbeat_score", "heartbeat_score >= 0 AND heartbeat_score <= 40");
                table.CheckConstraint("ck_device_health_scores_reconciliation_score", "reconciliation_score >= 0 AND reconciliation_score <= 30");
                table.CheckConstraint("ck_device_health_scores_resource_score", "resource_score >= 0 AND resource_score <= 30");
            });

        // Indexes for device_health_scores
        migrationBuilder.CreateIndex(
            name: "ix_device_health_scores_device_id",
            table: "device_health_scores",
            column: "device_id");

        migrationBuilder.CreateIndex(
            name: "ix_device_health_scores_timestamp",
            table: "device_health_scores",
            column: "timestamp");

        migrationBuilder.CreateIndex(
            name: "ix_device_health_scores_total_score",
            table: "device_health_scores",
            column: "total_score");

        // Convert to TimescaleDB hypertable for time-series optimization
        migrationBuilder.Sql(@"
            SELECT create_hypertable(
                'device_health_scores',
                'timestamp',
                chunk_time_interval => INTERVAL '1 day',
                if_not_exists => TRUE
            );
        ");

        // Add compression policy (compress data older than 7 days)
        migrationBuilder.Sql(@"
            ALTER TABLE device_health_scores SET (
                timescaledb.compress,
                timescaledb.compress_segmentby = 'device_id',
                timescaledb.compress_orderby = 'timestamp DESC'
            );
        ");

        migrationBuilder.Sql(@"
            SELECT add_compression_policy(
                'device_health_scores',
                INTERVAL '7 days',
                if_not_exists => TRUE
            );
        ");

        // Add retention policy (drop data older than 90 days)
        migrationBuilder.Sql(@"
            SELECT add_retention_policy(
                'device_health_scores',
                INTERVAL '90 days',
                if_not_exists => TRUE
            );
        ");

        // =====================================================
        // Alerts Table
        // =====================================================
        migrationBuilder.CreateTable(
            name: "alerts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                severity = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                title = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                description = table.Column<string>(type: "text", nullable: false),
                device_id = table.Column<Guid>(type: "uuid", nullable: true),
                rollout_id = table.Column<Guid>(type: "uuid", nullable: true),
                status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                acknowledged_by = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_alerts", x => x.id);
                table.CheckConstraint("ck_alerts_severity", "severity IN ('Info', 'Warning', 'Critical')");
                table.CheckConstraint("ck_alerts_status", "status IN ('Active', 'Acknowledged', 'Resolved')");
            });

        // Indexes for alerts
        migrationBuilder.CreateIndex(
            name: "ix_alerts_tenant_id",
            table: "alerts",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "ix_alerts_status",
            table: "alerts",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_alerts_created_at",
            table: "alerts",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "ix_alerts_device_id",
            table: "alerts",
            column: "device_id",
            filter: "device_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_alerts_type_severity",
            table: "alerts",
            columns: new[] { "type", "severity" });

        // Composite index for finding active alerts by device and type
        migrationBuilder.CreateIndex(
            name: "ix_alerts_device_type_status",
            table: "alerts",
            columns: new[] { "device_id", "type", "status" },
            filter: "device_id IS NOT NULL AND status = 'Active'");

        // =====================================================
        // AlertNotifications Table
        // =====================================================
        migrationBuilder.CreateTable(
            name: "alert_notifications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                alert_id = table.Column<Guid>(type: "uuid", nullable: false),
                channel = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                recipient = table.Column<string>(type: "text", nullable: false),
                sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                success = table.Column<bool>(type: "boolean", nullable: false),
                error = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_alert_notifications", x => x.id);
                table.ForeignKey(
                    name: "fk_alert_notifications_alerts_alert_id",
                    column: x => x.alert_id,
                    principalTable: "alerts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.CheckConstraint("ck_alert_notifications_channel", "channel IN ('Email', 'Slack', 'Teams', 'PagerDuty')");
            });

        // Indexes for alert_notifications
        migrationBuilder.CreateIndex(
            name: "ix_alert_notifications_alert_id",
            table: "alert_notifications",
            column: "alert_id");

        migrationBuilder.CreateIndex(
            name: "ix_alert_notifications_sent_at",
            table: "alert_notifications",
            column: "sent_at");

        migrationBuilder.CreateIndex(
            name: "ix_alert_notifications_success",
            table: "alert_notifications",
            column: "success");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Drop tables in reverse order due to foreign key constraints
        migrationBuilder.DropTable(name: "alert_notifications");
        migrationBuilder.DropTable(name: "alerts");
        migrationBuilder.DropTable(name: "device_health_scores");
    }
}
