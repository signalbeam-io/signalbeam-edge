using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for DeviceRegistrationToken entity.
/// </summary>
public class DeviceRegistrationTokenConfiguration : IEntityTypeConfiguration<DeviceRegistrationToken>
{
    public void Configure(EntityTypeBuilder<DeviceRegistrationToken> builder)
    {
        builder.ToTable("device_registration_tokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(t => t.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(
                v => v.Value,
                v => new TenantId(v))
            .IsRequired();

        builder.Property(t => t.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(t => t.TokenPrefix)
            .HasColumnName("token_prefix")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(t => t.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(t => t.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(256);

        builder.Property(t => t.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(t => t.MaxUses)
            .HasColumnName("max_uses");

        builder.Property(t => t.CurrentUses)
            .HasColumnName("current_uses")
            .IsRequired();

        builder.Property(t => t.IsRevoked)
            .HasColumnName("is_revoked")
            .IsRequired();

        builder.Property(t => t.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(t => t.RevokedBy)
            .HasColumnName("revoked_by")
            .HasMaxLength(256);

        // Legacy fields (kept for backward compatibility)
        builder.Property(t => t.IsUsed)
            .HasColumnName("is_used")
            .IsRequired();

        builder.Property(t => t.UsedAt)
            .HasColumnName("used_at");

        builder.Property(t => t.UsedByDeviceId)
            .HasColumnName("used_by_device_id")
            .HasConversion(
                v => v != null ? v.Value : (Guid?)null,
                v => v.HasValue ? new DeviceId(v.Value) : null);

        // Indexes
        builder.HasIndex(t => t.TokenPrefix)
            .HasDatabaseName("ix_device_registration_tokens_token_prefix");

        builder.HasIndex(t => t.TenantId)
            .HasDatabaseName("ix_device_registration_tokens_tenant_id");

        builder.HasIndex(t => new { t.IsRevoked, t.ExpiresAt })
            .HasDatabaseName("ix_device_registration_tokens_is_revoked_expires_at");

        // Ignore computed properties (not needed for this entity)
        builder.Ignore(t => t.IsValid);
        builder.Ignore(t => t.IsActive);
    }
}
