using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for DeviceDesiredState entity.
/// </summary>
public class DeviceDesiredStateConfiguration : IEntityTypeConfiguration<DeviceDesiredState>
{
    public void Configure(EntityTypeBuilder<DeviceDesiredState> builder)
    {
        builder.ToTable("device_desired_states");

        // Primary key
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("id");

        // DeviceId
        builder.Property(d => d.DeviceId)
            .HasConversion(
                id => id.Value,
                value => new DeviceId(value))
            .HasColumnName("device_id")
            .IsRequired();

        // BundleId
        builder.Property(d => d.BundleId)
            .HasConversion(
                id => id.Value,
                value => new BundleId(value))
            .HasColumnName("bundle_id")
            .IsRequired();

        // BundleVersion - stored as string
        builder.Property(d => d.BundleVersion)
            .HasConversion(
                v => v.ToString(),
                s => BundleVersion.Parse(s))
            .HasColumnName("bundle_version")
            .HasMaxLength(50)
            .IsRequired();

        // AssignedAt
        builder.Property(d => d.AssignedAt)
            .HasColumnName("assigned_at")
            .IsRequired();

        // AssignedBy
        builder.Property(d => d.AssignedBy)
            .HasColumnName("assigned_by")
            .HasMaxLength(200);

        // Indexes for performance
        builder.HasIndex(d => d.DeviceId)
            .IsUnique()
            .HasDatabaseName("ix_device_desired_states_device_id");

        builder.HasIndex(d => d.BundleId)
            .HasDatabaseName("ix_device_desired_states_bundle_id");

        builder.HasIndex(d => d.AssignedAt)
            .HasDatabaseName("ix_device_desired_states_assigned_at");
    }
}
