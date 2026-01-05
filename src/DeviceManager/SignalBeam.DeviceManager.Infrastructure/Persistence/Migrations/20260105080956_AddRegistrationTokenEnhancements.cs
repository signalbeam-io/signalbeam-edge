using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationTokenEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_device_registration_tokens_is_used_expires_at",
                schema: "device_manager",
                table: "device_registration_tokens");

            migrationBuilder.AddColumn<int>(
                name: "current_uses",
                schema: "device_manager",
                table: "device_registration_tokens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_revoked",
                schema: "device_manager",
                table: "device_registration_tokens",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "max_uses",
                schema: "device_manager",
                table: "device_registration_tokens",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "revoked_at",
                schema: "device_manager",
                table: "device_registration_tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "revoked_by",
                schema: "device_manager",
                table: "device_registration_tokens",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_registration_tokens_is_revoked_expires_at",
                schema: "device_manager",
                table: "device_registration_tokens",
                columns: new[] { "is_revoked", "expires_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_device_registration_tokens_is_revoked_expires_at",
                schema: "device_manager",
                table: "device_registration_tokens");

            migrationBuilder.DropColumn(
                name: "current_uses",
                schema: "device_manager",
                table: "device_registration_tokens");

            migrationBuilder.DropColumn(
                name: "is_revoked",
                schema: "device_manager",
                table: "device_registration_tokens");

            migrationBuilder.DropColumn(
                name: "max_uses",
                schema: "device_manager",
                table: "device_registration_tokens");

            migrationBuilder.DropColumn(
                name: "revoked_at",
                schema: "device_manager",
                table: "device_registration_tokens");

            migrationBuilder.DropColumn(
                name: "revoked_by",
                schema: "device_manager",
                table: "device_registration_tokens");

            migrationBuilder.CreateIndex(
                name: "ix_device_registration_tokens_is_used_expires_at",
                schema: "device_manager",
                table: "device_registration_tokens",
                columns: new[] { "is_used", "expires_at" });
        }
    }
}
