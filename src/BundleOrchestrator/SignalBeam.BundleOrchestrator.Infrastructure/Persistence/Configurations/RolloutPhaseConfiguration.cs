using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;

namespace SignalBeam.BundleOrchestrator.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for RolloutPhase entity.
/// </summary>
public class RolloutPhaseConfiguration : IEntityTypeConfiguration<RolloutPhase>
{
    public void Configure(EntityTypeBuilder<RolloutPhase> builder)
    {
        builder.ToTable("rollout_phases");

        // Primary key
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id");

        // RolloutId
        builder.Property(p => p.RolloutId)
            .HasColumnName("rollout_id")
            .IsRequired();

        // PhaseNumber
        builder.Property(p => p.PhaseNumber)
            .HasColumnName("phase_number")
            .IsRequired();

        // Name
        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        // TargetDeviceCount
        builder.Property(p => p.TargetDeviceCount)
            .HasColumnName("target_device_count")
            .IsRequired();

        // TargetPercentage
        builder.Property(p => p.TargetPercentage)
            .HasColumnName("target_percentage")
            .HasColumnType("decimal(5,2)");

        // Status
        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // StartedAt
        builder.Property(p => p.StartedAt)
            .HasColumnName("started_at");

        // CompletedAt
        builder.Property(p => p.CompletedAt)
            .HasColumnName("completed_at");

        // SuccessCount
        builder.Property(p => p.SuccessCount)
            .HasColumnName("success_count")
            .HasDefaultValue(0)
            .IsRequired();

        // FailureCount
        builder.Property(p => p.FailureCount)
            .HasColumnName("failure_count")
            .HasDefaultValue(0)
            .IsRequired();

        // MinHealthyDuration
        builder.Property(p => p.MinHealthyDuration)
            .HasColumnName("min_healthy_duration")
            .HasConversion(
                ts => ts == null ? null : (long?)ts.Value.TotalSeconds,
                seconds => seconds == null ? null : TimeSpan.FromSeconds(seconds.Value));

        // Relationships
        builder.HasMany<RolloutDeviceAssignment>()
            .WithOne()
            .HasForeignKey(a => a.PhaseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure the backing field for the collection
        builder.Navigation(p => p.DeviceAssignments)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_deviceAssignments");

        // Indexes
        builder.HasIndex(p => p.RolloutId)
            .HasDatabaseName("ix_rollout_phases_rollout_id");

        builder.HasIndex(p => new { p.RolloutId, p.PhaseNumber })
            .IsUnique()
            .HasDatabaseName("ix_rollout_phases_rollout_id_phase_number");

        builder.HasIndex(p => p.Status)
            .HasDatabaseName("ix_rollout_phases_status");
    }
}
