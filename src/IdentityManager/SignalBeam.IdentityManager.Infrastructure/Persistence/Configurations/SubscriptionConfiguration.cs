using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.IdentityManager.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Subscription entity.
/// </summary>
public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions");

        // Primary key
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id");

        // TenantId
        builder.Property(s => s.TenantId)
            .HasConversion(
                id => id.Value,
                value => new TenantId(value))
            .HasColumnName("tenant_id")
            .IsRequired();

        // Tier
        builder.Property(s => s.Tier)
            .HasColumnName("tier")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // Status
        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // DeviceCount
        builder.Property(s => s.DeviceCount)
            .HasColumnName("device_count")
            .HasDefaultValue(0)
            .IsRequired();

        // Timestamps
        builder.Property(s => s.StartedAt)
            .HasColumnName("started_at")
            .IsRequired();

        builder.Property(s => s.EndedAt)
            .HasColumnName("ended_at");

        // Indexes
        builder.HasIndex(s => s.TenantId)
            .HasDatabaseName("ix_subscriptions_tenant_id");

        builder.HasIndex(s => s.Status)
            .HasDatabaseName("ix_subscriptions_status");

        // Ignore computed properties (MaxDevices, DataRetentionDays are calculated from Tier)
        builder.Ignore(s => s.MaxDevices);
        builder.Ignore(s => s.DataRetentionDays);
    }
}
