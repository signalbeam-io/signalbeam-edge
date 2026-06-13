using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationTokenAutoApprove : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "auto_approve",
                schema: "device_manager",
                table: "device_registration_tokens",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "auto_approve",
                schema: "device_manager",
                table: "device_registration_tokens");
        }
    }
}
