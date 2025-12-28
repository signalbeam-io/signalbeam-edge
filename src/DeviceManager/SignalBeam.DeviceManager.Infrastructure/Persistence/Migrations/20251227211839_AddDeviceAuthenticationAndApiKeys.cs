using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceAuthenticationAndApiKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "registration_status",
                schema: "device_manager",
                table: "devices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.CreateTable(
                name: "device_api_keys",
                schema: "device_manager",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    key_prefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_api_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "device_authentication_logs",
                schema: "device_manager",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    api_key_prefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_authentication_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "device_certificates",
                schema: "device_manager",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    certificate = table.Column<string>(type: "text", nullable: false),
                    serial_number = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_certificates", x => x.id);
                });

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
                name: "ix_device_api_keys_device_id",
                schema: "device_manager",
                table: "device_api_keys",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_api_keys_device_revoked",
                schema: "device_manager",
                table: "device_api_keys",
                columns: new[] { "device_id", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "ix_device_api_keys_prefix",
                schema: "device_manager",
                table: "device_api_keys",
                column: "key_prefix");

            migrationBuilder.CreateIndex(
                name: "ix_device_authentication_logs_device_id",
                schema: "device_manager",
                table: "device_authentication_logs",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_authentication_logs_success_timestamp",
                schema: "device_manager",
                table: "device_authentication_logs",
                columns: new[] { "success", "timestamp" });

            migrationBuilder.CreateIndex(
                name: "ix_device_authentication_logs_timestamp",
                schema: "device_manager",
                table: "device_authentication_logs",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "ix_device_certificates_device_id",
                schema: "device_manager",
                table: "device_certificates",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_certificates_fingerprint",
                schema: "device_manager",
                table: "device_certificates",
                column: "fingerprint");

            migrationBuilder.CreateIndex(
                name: "ix_device_certificates_serial_number",
                schema: "device_manager",
                table: "device_certificates",
                column: "serial_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_registration_tokens_token_prefix",
                schema: "device_manager",
                table: "device_registration_tokens",
                column: "token_prefix");

            migrationBuilder.CreateIndex(
                name: "ix_device_registration_tokens_tenant_id",
                schema: "device_manager",
                table: "device_registration_tokens",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_registration_tokens_is_used_expires_at",
                schema: "device_manager",
                table: "device_registration_tokens",
                columns: new[] { "is_used", "expires_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_api_keys",
                schema: "device_manager");

            migrationBuilder.DropTable(
                name: "device_authentication_logs",
                schema: "device_manager");

            migrationBuilder.DropTable(
                name: "device_certificates",
                schema: "device_manager");

            migrationBuilder.DropTable(
                name: "device_registration_tokens",
                schema: "device_manager");

            migrationBuilder.DropColumn(
                name: "registration_status",
                schema: "device_manager",
                table: "devices");
        }
    }
}
