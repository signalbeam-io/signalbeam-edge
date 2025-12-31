using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceGroupMembershipsAndDynamicGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tag_query",
                schema: "device_manager",
                table: "device_groups",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type",
                schema: "device_manager",
                table: "device_groups",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Static"); // Default to Static for existing groups

            migrationBuilder.CreateTable(
                name: "device_group_memberships",
                schema: "device_manager",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    added_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_group_memberships", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_device_group_memberships_device_id",
                schema: "device_manager",
                table: "device_group_memberships",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_group_memberships_group_device_unique",
                schema: "device_manager",
                table: "device_group_memberships",
                columns: new[] { "group_id", "device_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_group_memberships_group_id",
                schema: "device_manager",
                table: "device_group_memberships",
                column: "group_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_group_memberships",
                schema: "device_manager");

            migrationBuilder.DropColumn(
                name: "tag_query",
                schema: "device_manager",
                table: "device_groups");

            migrationBuilder.DropColumn(
                name: "type",
                schema: "device_manager",
                table: "device_groups");
        }
    }
}
