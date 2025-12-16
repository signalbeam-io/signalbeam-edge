using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalBeam.DeviceManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateWithTimescaleDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "device_manager");

            migrationBuilder.CreateTable(
                name: "device_activity_logs",
                schema: "device_manager",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    activity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_activity_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "device_groups",
                schema: "device_manager",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tag_criteria = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "device_heartbeats",
                schema: "device_manager",
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
                schema: "device_manager",
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
                name: "devices",
                schema: "device_manager",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    assigned_bundle_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bundle_deployment_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    device_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tags = table.Column<List<string>>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_device_activity_logs_activity_type",
                schema: "device_manager",
                table: "device_activity_logs",
                column: "activity_type");

            migrationBuilder.CreateIndex(
                name: "ix_device_activity_logs_device_id",
                schema: "device_manager",
                table: "device_activity_logs",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_activity_logs_device_timestamp",
                schema: "device_manager",
                table: "device_activity_logs",
                columns: new[] { "device_id", "timestamp" });

            migrationBuilder.CreateIndex(
                name: "ix_device_activity_logs_timestamp",
                schema: "device_manager",
                table: "device_activity_logs",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "ix_device_groups_tenant_id",
                schema: "device_manager",
                table: "device_groups",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_groups_tenant_name",
                schema: "device_manager",
                table: "device_groups",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_heartbeats_device_id",
                schema: "device_manager",
                table: "device_heartbeats",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_heartbeats_device_timestamp",
                schema: "device_manager",
                table: "device_heartbeats",
                columns: new[] { "device_id", "timestamp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_device_heartbeats_timestamp",
                schema: "device_manager",
                table: "device_heartbeats",
                column: "timestamp",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_device_metrics_device_id",
                schema: "device_manager",
                table: "device_metrics",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_metrics_device_timestamp",
                schema: "device_manager",
                table: "device_metrics",
                columns: new[] { "device_id", "timestamp" });

            migrationBuilder.CreateIndex(
                name: "ix_device_metrics_timestamp",
                schema: "device_manager",
                table: "device_metrics",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "ix_devices_last_seen_at",
                schema: "device_manager",
                table: "devices",
                column: "last_seen_at");

            migrationBuilder.CreateIndex(
                name: "ix_devices_status",
                schema: "device_manager",
                table: "devices",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_devices_tenant_id",
                schema: "device_manager",
                table: "devices",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_activity_logs",
                schema: "device_manager");

            migrationBuilder.DropTable(
                name: "device_groups",
                schema: "device_manager");

            migrationBuilder.DropTable(
                name: "device_heartbeats",
                schema: "device_manager");

            migrationBuilder.DropTable(
                name: "device_metrics",
                schema: "device_manager");

            migrationBuilder.DropTable(
                name: "devices",
                schema: "device_manager");
        }
    }
}
