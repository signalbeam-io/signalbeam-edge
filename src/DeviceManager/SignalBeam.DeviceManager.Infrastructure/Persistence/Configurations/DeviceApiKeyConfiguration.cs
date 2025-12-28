using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for DeviceApiKey entity.
/// </summary>
public class DeviceApiKeyConfiguration : IEntityTypeConfiguration<DeviceApiKey>
{
    public void Configure(EntityTypeBuilder<DeviceApiKey> builder)
    {
        builder.ToTable("device_api_keys");

        // Primary key
        builder.HasKey(k => k.Id);

        builder.Property(k => k.Id)
            .HasColumnName("id")
            .IsRequired();

        // DeviceId (foreign key)
        builder.Property(k => k.DeviceId)
            .HasConversion(
                id => id.Value,
                value => new DeviceId(value))
            .HasColumnName("device_id")
            .IsRequired();

        // KeyHash
        builder.Property(k => k.KeyHash)
            .HasColumnName("key_hash")
            .HasMaxLength(255)
            .IsRequired();

        // KeyPrefix
        builder.Property(k => k.KeyPrefix)
            .HasColumnName("key_prefix")
            .HasMaxLength(10)
            .IsRequired();

        // Timestamps
        builder.Property(k => k.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(k => k.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(k => k.LastUsedAt)
            .HasColumnName("last_used_at");

        builder.Property(k => k.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(k => k.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(255);

        // Indexes for performance
        builder.HasIndex(k => k.DeviceId)
            .HasDatabaseName("ix_device_api_keys_device_id");

        builder.HasIndex(k => k.KeyPrefix)
            .HasDatabaseName("ix_device_api_keys_prefix");

        builder.HasIndex(k => new { k.DeviceId, k.RevokedAt })
            .HasDatabaseName("ix_device_api_keys_device_revoked");
    }
}
