using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for RolloutDeviceAssignment entity.
/// </summary>
public class RolloutDeviceAssignmentConfiguration : IEntityTypeConfiguration<RolloutDeviceAssignment>
{
    public void Configure(EntityTypeBuilder<RolloutDeviceAssignment> builder)
    {
        builder.ToTable("rollout_device_assignments");

        // Primary key
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id");

        // RolloutId
        builder.Property(a => a.RolloutId)
            .HasColumnName("rollout_id")
            .IsRequired();

        // PhaseId
        builder.Property(a => a.PhaseId)
            .HasColumnName("phase_id")
            .IsRequired();

        // DeviceId
        builder.Property(a => a.DeviceId)
            .HasConversion(
                id => id.Value,
                value => new DeviceId(value))
            .HasColumnName("device_id")
            .IsRequired();

        // Status
        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // AssignedAt
        builder.Property(a => a.AssignedAt)
            .HasColumnName("assigned_at");

        // ReconciledAt
        builder.Property(a => a.ReconciledAt)
            .HasColumnName("reconciled_at");

        // ErrorMessage
        builder.Property(a => a.ErrorMessage)
            .HasColumnName("error_message")
            .HasColumnType("text");

        // RetryCount
        builder.Property(a => a.RetryCount)
            .HasColumnName("retry_count")
            .HasDefaultValue(0)
            .IsRequired();

        // Indexes
        builder.HasIndex(a => a.RolloutId)
            .HasDatabaseName("ix_rollout_device_assignments_rollout_id");

        builder.HasIndex(a => a.PhaseId)
            .HasDatabaseName("ix_rollout_device_assignments_phase_id");

        builder.HasIndex(a => a.DeviceId)
            .HasDatabaseName("ix_rollout_device_assignments_device_id");

        builder.HasIndex(a => new { a.RolloutId, a.DeviceId })
            .IsUnique()
            .HasDatabaseName("ix_rollout_device_assignments_rollout_id_device_id");

        builder.HasIndex(a => a.Status)
            .HasDatabaseName("ix_rollout_device_assignments_status");

        builder.HasIndex(a => new { a.PhaseId, a.Status })
            .HasDatabaseName("ix_rollout_device_assignments_phase_id_status");
    }
}
