using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.IdentityManager.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Tenant entity.
/// </summary>
public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        // Primary key
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasConversion(
                id => id.Value,
                value => new TenantId(value))
            .HasColumnName("id");

        // Name
        builder.Property(t => t.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        // Slug (unique URL-friendly identifier)
        builder.Property(t => t.Slug)
            .HasColumnName("slug")
            .HasMaxLength(64)
            .IsRequired();

        // SubscriptionTier
        builder.Property(t => t.SubscriptionTier)
            .HasColumnName("subscription_tier")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // Status
        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // MaxDevices
        builder.Property(t => t.MaxDevices)
            .HasColumnName("max_devices")
            .IsRequired();

        // DataRetentionDays
        builder.Property(t => t.DataRetentionDays)
            .HasColumnName("data_retention_days")
            .IsRequired();

        // Timestamps
        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(t => t.UpgradedAt)
            .HasColumnName("upgraded_at");

        // Indexes
        builder.HasIndex(t => t.Slug)
            .IsUnique()
            .HasDatabaseName("uq_tenants_slug");

        builder.HasIndex(t => t.Status)
            .HasDatabaseName("ix_tenants_status");

        // Ignore domain events collection
        builder.Ignore(t => t.DomainEvents);
    }
}
