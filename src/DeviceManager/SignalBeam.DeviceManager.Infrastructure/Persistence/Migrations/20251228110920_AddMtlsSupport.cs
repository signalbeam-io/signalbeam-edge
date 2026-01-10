using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMtlsSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Subject",
                schema: "device_manager",
                table: "device_certificates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Type",
                schema: "device_manager",
                table: "device_certificates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Note: device_registration_tokens table is created in 20251227211839_AddDeviceAuthenticationAndApiKeys migration
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Subject",
                schema: "device_manager",
                table: "device_certificates");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "device_manager",
                table: "device_certificates");
        }
    }
}
