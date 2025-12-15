using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Configurations;

public class DeviceActivityLogConfiguration : IEntityTypeConfiguration<DeviceActivityLog>
{
    public void Configure(EntityTypeBuilder<DeviceActivityLog> builder)
    {
        builder.ToTable("device_activity_logs");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("id")
            .IsRequired();

        // DeviceId value object conversion
        builder.Property(l => l.DeviceId)
            .HasColumnName("device_id")
            .HasConversion(
                v => v.Value,
                v => new DeviceId(v))
            .IsRequired();

        builder.Property(l => l.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        builder.Property(l => l.ActivityType)
            .HasColumnName("activity_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(l => l.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(l => l.Severity)
            .HasColumnName("severity")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(l => l.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        // Indexes for querying logs
        builder.HasIndex(l => l.DeviceId)
            .HasDatabaseName("ix_device_activity_logs_device_id");

        builder.HasIndex(l => l.Timestamp)
            .HasDatabaseName("ix_device_activity_logs_timestamp");

        builder.HasIndex(l => new { l.DeviceId, l.Timestamp })
            .HasDatabaseName("ix_device_activity_logs_device_timestamp");

        builder.HasIndex(l => l.ActivityType)
            .HasDatabaseName("ix_device_activity_logs_activity_type");
    }
}
