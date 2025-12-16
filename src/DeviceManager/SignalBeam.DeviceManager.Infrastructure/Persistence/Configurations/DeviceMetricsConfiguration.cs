using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Configurations;

public class DeviceMetricsConfiguration : IEntityTypeConfiguration<DeviceMetrics>
{
    public void Configure(EntityTypeBuilder<DeviceMetrics> builder)
    {
        builder.ToTable("device_metrics", tb =>
            tb.HasComment("TimescaleDB hypertable for device metrics"));

        // TimescaleDB requires primary key to include partitioning column (timestamp)
        builder.HasKey(m => new { m.Id, m.Timestamp });

        builder.Property(m => m.Id)
            .HasColumnName("id")
            .IsRequired();

        // DeviceId value object conversion
        builder.Property(m => m.DeviceId)
            .HasColumnName("device_id")
            .HasConversion(
                v => v.Value,
                v => new DeviceId(v))
            .IsRequired();

        builder.Property(m => m.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        builder.Property(m => m.CpuUsage)
            .HasColumnName("cpu_usage")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(m => m.MemoryUsage)
            .HasColumnName("memory_usage")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(m => m.DiskUsage)
            .HasColumnName("disk_usage")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(m => m.UptimeSeconds)
            .HasColumnName("uptime_seconds")
            .IsRequired();

        builder.Property(m => m.RunningContainers)
            .HasColumnName("running_containers")
            .IsRequired();

        builder.Property(m => m.AdditionalMetrics)
            .HasColumnName("additional_metrics")
            .HasColumnType("jsonb");

        // Indexes for time-series queries
        builder.HasIndex(m => m.DeviceId)
            .HasDatabaseName("ix_device_metrics_device_id");

        builder.HasIndex(m => m.Timestamp)
            .HasDatabaseName("ix_device_metrics_timestamp");

        builder.HasIndex(m => new { m.DeviceId, m.Timestamp })
            .HasDatabaseName("ix_device_metrics_device_timestamp");
    }
}
