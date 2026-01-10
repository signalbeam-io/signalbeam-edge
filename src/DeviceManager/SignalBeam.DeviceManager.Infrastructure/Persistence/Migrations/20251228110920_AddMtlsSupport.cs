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

            migrationBuilder.CreateTable(
                name: "device_registration_tokens",
                schema: "device_manager",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    token_prefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_used = table.Column<bool>(type: "boolean", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    used_by_device_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_registration_tokens", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_device_registration_tokens_is_used_expires_at",
                schema: "device_manager",
                table: "device_registration_tokens",
                columns: new[] { "is_used", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_device_registration_tokens_tenant_id",
                schema: "device_manager",
                table: "device_registration_tokens",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_registration_tokens_token_prefix",
                schema: "device_manager",
                table: "device_registration_tokens",
                column: "token_prefix");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_registration_tokens",
                schema: "device_manager");

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
