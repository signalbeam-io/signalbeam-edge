using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Rollout entity.
/// </summary>
public class RolloutConfiguration : IEntityTypeConfiguration<Rollout>
{
    public void Configure(EntityTypeBuilder<Rollout> builder)
    {
        builder.ToTable("rollouts");

        // Primary key
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id");

        // TenantId
        builder.Property(r => r.TenantId)
            .HasConversion(
                id => id.Value,
                value => new TenantId(value))
            .HasColumnName("tenant_id")
            .IsRequired();

        // BundleId
        builder.Property(r => r.BundleId)
            .HasConversion(
                id => id.Value,
                value => new BundleId(value))
            .HasColumnName("bundle_id")
            .IsRequired();

        // TargetVersion
        builder.Property(r => r.TargetVersion)
            .HasConversion(
                v => v.ToString(),
                s => BundleVersion.Parse(s))
            .HasColumnName("target_version")
            .HasMaxLength(50)
            .IsRequired();

        // PreviousVersion
        builder.Property(r => r.PreviousVersion)
            .HasConversion(
                v => v == null ? null : v.ToString(),
                s => s == null ? null : BundleVersion.Parse(s))
            .HasColumnName("previous_version")
            .HasMaxLength(50);

        // Name
        builder.Property(r => r.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        // Description
        builder.Property(r => r.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        // Status
        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // TargetDeviceGroupId
        builder.Property(r => r.TargetDeviceGroupId)
            .HasColumnName("target_device_group_id");

        // CreatedBy
        builder.Property(r => r.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(255);

        // CreatedAt
        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // StartedAt
        builder.Property(r => r.StartedAt)
            .HasColumnName("started_at");

        // CompletedAt
        builder.Property(r => r.CompletedAt)
            .HasColumnName("completed_at");

        // FailureThreshold
        builder.Property(r => r.FailureThreshold)
            .HasColumnName("failure_threshold")
            .HasColumnType("decimal(5,4)")
            .HasDefaultValue(0.05m)
            .IsRequired();

        // CurrentPhaseNumber
        builder.Property(r => r.CurrentPhaseNumber)
            .HasColumnName("current_phase_number")
            .HasDefaultValue(0)
            .IsRequired();

        // Relationships - use navigation property instead of backing field
        builder.HasMany<RolloutPhase>()
            .WithOne()
            .HasForeignKey(p => p.RolloutId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure the backing field for the collection
        builder.Navigation(r => r.Phases)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_phases");

        // Indexes
        builder.HasIndex(r => r.TenantId)
            .HasDatabaseName("ix_rollouts_tenant_id");

        builder.HasIndex(r => r.BundleId)
            .HasDatabaseName("ix_rollouts_bundle_id");

        builder.HasIndex(r => r.Status)
            .HasDatabaseName("ix_rollouts_status");

        builder.HasIndex(r => new { r.TenantId, r.Status })
            .HasDatabaseName("ix_rollouts_tenant_id_status");

        builder.HasIndex(r => r.CreatedAt)
            .HasDatabaseName("ix_rollouts_created_at");

        builder.HasIndex(r => r.StartedAt)
            .HasDatabaseName("ix_rollouts_started_at");

        // Ignore domain events collection (not persisted)
        builder.Ignore("DomainEvents");
    }
}
