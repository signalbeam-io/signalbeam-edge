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
            // Check if columns exist before adding them (idempotent)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'device_manager'
                        AND table_name = 'device_certificates'
                        AND column_name = 'Subject'
                    ) THEN
                        ALTER TABLE device_manager.device_certificates
                        ADD COLUMN ""Subject"" text NOT NULL DEFAULT '';
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'device_manager'
                        AND table_name = 'device_certificates'
                        AND column_name = 'Type'
                    ) THEN
                        ALTER TABLE device_manager.device_certificates
                        ADD COLUMN ""Type"" integer NOT NULL DEFAULT 0;
                    END IF;
                END $$;
            ");

            // Create table only if it doesn't exist (idempotent)
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS device_manager.device_registration_tokens (
                    id uuid NOT NULL,
                    tenant_id uuid NOT NULL,
                    token_hash character varying(128) NOT NULL,
                    token_prefix character varying(16) NOT NULL,
                    created_at timestamp with time zone NOT NULL,
                    expires_at timestamp with time zone NOT NULL,
                    created_by character varying(256),
                    description character varying(500),
                    is_used boolean NOT NULL,
                    used_at timestamp with time zone,
                    used_by_device_id uuid,
                    CONSTRAINT ""PK_device_registration_tokens"" PRIMARY KEY (id)
                );
            ");

            // Create indexes only if they don't exist (idempotent)
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_device_registration_tokens_is_used_expires_at
                ON device_manager.device_registration_tokens (is_used, expires_at);

                CREATE INDEX IF NOT EXISTS ix_device_registration_tokens_tenant_id
                ON device_manager.device_registration_tokens (tenant_id);

                CREATE INDEX IF NOT EXISTS ix_device_registration_tokens_token_prefix
                ON device_manager.device_registration_tokens (token_prefix);
            ");
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
