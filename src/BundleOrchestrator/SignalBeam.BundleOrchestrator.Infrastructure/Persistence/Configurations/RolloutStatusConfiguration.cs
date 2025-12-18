using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for RolloutStatus entity.
/// </summary>
public class RolloutStatusConfiguration : IEntityTypeConfiguration<RolloutStatus>
{
    public void Configure(EntityTypeBuilder<RolloutStatus> builder)
    {
        builder.ToTable("rollout_statuses");

        // Primary key
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id");

        // BundleId
        builder.Property(r => r.BundleId)
            .HasConversion(
                id => id.Value,
                value => new BundleId(value))
            .HasColumnName("bundle_id")
            .IsRequired();

        // BundleVersion - stored as string
        builder.Property(r => r.BundleVersion)
            .HasConversion(
                v => v.ToString(),
                s => BundleVersion.Parse(s))
            .HasColumnName("bundle_version")
            .HasMaxLength(50)
            .IsRequired();

        // DeviceId
        builder.Property(r => r.DeviceId)
            .HasConversion(
                id => id.Value,
                value => new DeviceId(value))
            .HasColumnName("device_id")
            .IsRequired();

        // Status
        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // StartedAt
        builder.Property(r => r.StartedAt)
            .HasColumnName("started_at")
            .IsRequired();

        // CompletedAt
        builder.Property(r => r.CompletedAt)
            .HasColumnName("completed_at");

        // ErrorMessage
        builder.Property(r => r.ErrorMessage)
            .HasColumnName("error_message")
            .HasColumnType("text");

        // RetryCount
        builder.Property(r => r.RetryCount)
            .HasColumnName("retry_count")
            .HasDefaultValue(0)
            .IsRequired();

        // Indexes for performance
        builder.HasIndex(r => new { r.DeviceId, r.BundleId, r.BundleVersion })
            .IsUnique()
            .HasDatabaseName("ix_rollout_statuses_device_bundle_version");

        builder.HasIndex(r => r.Status)
            .HasDatabaseName("ix_rollout_statuses_status");

        builder.HasIndex(r => r.StartedAt)
            .HasDatabaseName("ix_rollout_statuses_started_at");

        builder.HasIndex(r => r.CompletedAt)
            .HasDatabaseName("ix_rollout_statuses_completed_at");
    }
}
