using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalBeam.IdentityManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedDefaultTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Insert default tenant for MVP
            migrationBuilder.Sql(@"
                INSERT INTO identity.tenants (id, name, slug, subscription_tier, status, max_devices, data_retention_days, created_at, upgraded_at)
                VALUES (
                    '00000000-0000-0000-0000-000000000001'::uuid,
                    'Default MVP Tenant',
                    'mvp-default',
                    'Paid',
                    'Active',
                    2147483647,  -- int.MaxValue for unlimited devices
                    90,          -- 90 days data retention for Paid tier
                    NOW(),
                    NULL
                )
                ON CONFLICT (id) DO NOTHING;
            ");

            // Insert default subscription for MVP tenant
            migrationBuilder.Sql(@"
                INSERT INTO identity.subscriptions (id, tenant_id, tier, status, device_count, started_at, ended_at)
                VALUES (
                    '00000000-0000-0000-0000-000000000002'::uuid,
                    '00000000-0000-0000-0000-000000000001'::uuid,
                    'Paid',
                    'Active',
                    0,   -- Start with 0 devices
                    NOW(),
                    NULL
                )
                ON CONFLICT (id) DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove default subscription
            migrationBuilder.Sql(@"
                DELETE FROM identity.subscriptions
                WHERE id = '00000000-0000-0000-0000-000000000002'::uuid;
            ");

            // Remove default tenant
            migrationBuilder.Sql(@"
                DELETE FROM identity.tenants
                WHERE id = '00000000-0000-0000-0000-000000000001'::uuid;
            ");
        }
    }
}
