using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalBeam.BundleOrchestrator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRolloutIdToRolloutStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "rollout_id",
                schema: "bundle_orchestrator",
                table: "rollout_statuses",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // For existing rows, set rollout_id = id (each existing entry is its own rollout)
            migrationBuilder.Sql(
                @"UPDATE bundle_orchestrator.rollout_statuses
                  SET rollout_id = id
                  WHERE rollout_id = '00000000-0000-0000-0000-000000000000'");

            migrationBuilder.CreateIndex(
                name: "ix_rollout_statuses_rollout_id",
                schema: "bundle_orchestrator",
                table: "rollout_statuses",
                column: "rollout_id");

            migrationBuilder.CreateIndex(
                name: "ix_rollout_statuses_rollout_id_status",
                schema: "bundle_orchestrator",
                table: "rollout_statuses",
                columns: new[] { "rollout_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_rollout_statuses_rollout_id",
                schema: "bundle_orchestrator",
                table: "rollout_statuses");

            migrationBuilder.DropIndex(
                name: "ix_rollout_statuses_rollout_id_status",
                schema: "bundle_orchestrator",
                table: "rollout_statuses");

            migrationBuilder.DropColumn(
                name: "rollout_id",
                schema: "bundle_orchestrator",
                table: "rollout_statuses");
        }
    }
}
