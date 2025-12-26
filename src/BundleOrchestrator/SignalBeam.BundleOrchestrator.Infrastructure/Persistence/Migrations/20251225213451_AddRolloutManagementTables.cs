using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalBeam.BundleOrchestrator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRolloutManagementTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "blob_storage_uri",
                schema: "bundle_orchestrator",
                table: "app_bundle_versions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "checksum",
                schema: "bundle_orchestrator",
                table: "app_bundle_versions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "size_bytes",
                schema: "bundle_orchestrator",
                table: "app_bundle_versions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "bundle_orchestrator",
                table: "app_bundle_versions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "rollouts",
                schema: "bundle_orchestrator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bundle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    previous_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target_device_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_threshold = table.Column<decimal>(type: "numeric(5,4)", nullable: false, defaultValue: 0.05m),
                    current_phase_number = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rollouts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rollout_phases",
                schema: "bundle_orchestrator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rollout_id = table.Column<Guid>(type: "uuid", nullable: false),
                    phase_number = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    target_device_count = table.Column<int>(type: "integer", nullable: false),
                    target_percentage = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    success_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    failure_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    min_healthy_duration = table.Column<long>(type: "bigint", nullable: true),
                    RolloutId1 = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rollout_phases", x => x.id);
                    table.ForeignKey(
                        name: "FK_rollout_phases_rollouts_RolloutId1",
                        column: x => x.RolloutId1,
                        principalSchema: "bundle_orchestrator",
                        principalTable: "rollouts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_rollout_phases_rollouts_rollout_id",
                        column: x => x.rollout_id,
                        principalSchema: "bundle_orchestrator",
                        principalTable: "rollouts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rollout_device_assignments",
                schema: "bundle_orchestrator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rollout_id = table.Column<Guid>(type: "uuid", nullable: false),
                    phase_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reconciled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RolloutPhaseId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rollout_device_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_rollout_device_assignments_rollout_phases_RolloutPhaseId",
                        column: x => x.RolloutPhaseId,
                        principalSchema: "bundle_orchestrator",
                        principalTable: "rollout_phases",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_rollout_device_assignments_rollout_phases_phase_id",
                        column: x => x.phase_id,
                        principalSchema: "bundle_orchestrator",
                        principalTable: "rollout_phases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_app_bundle_versions_status",
                schema: "bundle_orchestrator",
                table: "app_bundle_versions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_rollout_device_assignments_device_id",
                schema: "bundle_orchestrator",
                table: "rollout_device_assignments",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_rollout_device_assignments_phase_id",
                schema: "bundle_orchestrator",
                table: "rollout_device_assignments",
                column: "phase_id");

            migrationBuilder.CreateIndex(
                name: "ix_rollout_device_assignments_phase_id_status",
                schema: "bundle_orchestrator",
                table: "rollout_device_assignments",
                columns: new[] { "phase_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_rollout_device_assignments_rollout_id",
                schema: "bundle_orchestrator",
                table: "rollout_device_assignments",
                column: "rollout_id");

            migrationBuilder.CreateIndex(
                name: "ix_rollout_device_assignments_rollout_id_device_id",
                schema: "bundle_orchestrator",
                table: "rollout_device_assignments",
                columns: new[] { "rollout_id", "device_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rollout_device_assignments_RolloutPhaseId",
                schema: "bundle_orchestrator",
                table: "rollout_device_assignments",
                column: "RolloutPhaseId");

            migrationBuilder.CreateIndex(
                name: "ix_rollout_device_assignments_status",
                schema: "bundle_orchestrator",
                table: "rollout_device_assignments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_rollout_phases_rollout_id",
                schema: "bundle_orchestrator",
                table: "rollout_phases",
                column: "rollout_id");

            migrationBuilder.CreateIndex(
                name: "ix_rollout_phases_rollout_id_phase_number",
                schema: "bundle_orchestrator",
                table: "rollout_phases",
                columns: new[] { "rollout_id", "phase_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rollout_phases_RolloutId1",
                schema: "bundle_orchestrator",
                table: "rollout_phases",
                column: "RolloutId1");

            migrationBuilder.CreateIndex(
                name: "ix_rollout_phases_status",
                schema: "bundle_orchestrator",
                table: "rollout_phases",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_rollouts_bundle_id",
                schema: "bundle_orchestrator",
                table: "rollouts",
                column: "bundle_id");

            migrationBuilder.CreateIndex(
                name: "ix_rollouts_created_at",
                schema: "bundle_orchestrator",
                table: "rollouts",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_rollouts_started_at",
                schema: "bundle_orchestrator",
                table: "rollouts",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "ix_rollouts_status",
                schema: "bundle_orchestrator",
                table: "rollouts",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_rollouts_tenant_id",
                schema: "bundle_orchestrator",
                table: "rollouts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_rollouts_tenant_id_status",
                schema: "bundle_orchestrator",
                table: "rollouts",
                columns: new[] { "tenant_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rollout_device_assignments",
                schema: "bundle_orchestrator");

            migrationBuilder.DropTable(
                name: "rollout_phases",
                schema: "bundle_orchestrator");

            migrationBuilder.DropTable(
                name: "rollouts",
                schema: "bundle_orchestrator");

            migrationBuilder.DropIndex(
                name: "ix_app_bundle_versions_status",
                schema: "bundle_orchestrator",
                table: "app_bundle_versions");

            migrationBuilder.DropColumn(
                name: "blob_storage_uri",
                schema: "bundle_orchestrator",
                table: "app_bundle_versions");

            migrationBuilder.DropColumn(
                name: "checksum",
                schema: "bundle_orchestrator",
                table: "app_bundle_versions");

            migrationBuilder.DropColumn(
                name: "size_bytes",
                schema: "bundle_orchestrator",
                table: "app_bundle_versions");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "bundle_orchestrator",
                table: "app_bundle_versions");
        }
    }
}
