using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalBeam.BundleOrchestrator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "bundle_orchestrator");

            migrationBuilder.CreateTable(
                name: "app_bundle_versions",
                schema: "bundle_orchestrator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bundle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    containers = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    release_notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_bundle_versions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "app_bundles",
                schema: "bundle_orchestrator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    latest_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_bundles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "device_desired_states",
                schema: "bundle_orchestrator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bundle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bundle_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    assigned_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_desired_states", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rollout_statuses",
                schema: "bundle_orchestrator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bundle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bundle_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rollout_statuses", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_app_bundle_versions_bundle_id",
                schema: "bundle_orchestrator",
                table: "app_bundle_versions",
                column: "bundle_id");

            migrationBuilder.CreateIndex(
                name: "ix_app_bundle_versions_bundle_id_version",
                schema: "bundle_orchestrator",
                table: "app_bundle_versions",
                columns: new[] { "bundle_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_app_bundle_versions_created_at",
                schema: "bundle_orchestrator",
                table: "app_bundle_versions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_app_bundles_tenant_id",
                schema: "bundle_orchestrator",
                table: "app_bundles",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_app_bundles_tenant_id_name",
                schema: "bundle_orchestrator",
                table: "app_bundles",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_desired_states_assigned_at",
                schema: "bundle_orchestrator",
                table: "device_desired_states",
                column: "assigned_at");

            migrationBuilder.CreateIndex(
                name: "ix_device_desired_states_bundle_id",
                schema: "bundle_orchestrator",
                table: "device_desired_states",
                column: "bundle_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_desired_states_device_id",
                schema: "bundle_orchestrator",
                table: "device_desired_states",
                column: "device_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rollout_statuses_completed_at",
                schema: "bundle_orchestrator",
                table: "rollout_statuses",
                column: "completed_at");

            migrationBuilder.CreateIndex(
                name: "ix_rollout_statuses_device_bundle_version",
                schema: "bundle_orchestrator",
                table: "rollout_statuses",
                columns: new[] { "device_id", "bundle_id", "bundle_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rollout_statuses_started_at",
                schema: "bundle_orchestrator",
                table: "rollout_statuses",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "ix_rollout_statuses_status",
                schema: "bundle_orchestrator",
                table: "rollout_statuses",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_bundle_versions",
                schema: "bundle_orchestrator");

            migrationBuilder.DropTable(
                name: "app_bundles",
                schema: "bundle_orchestrator");

            migrationBuilder.DropTable(
                name: "device_desired_states",
                schema: "bundle_orchestrator");

            migrationBuilder.DropTable(
                name: "rollout_statuses",
                schema: "bundle_orchestrator");
        }
    }
}
