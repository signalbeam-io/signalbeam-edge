using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for DeviceHealthScore entity.
/// Configures as TimescaleDB hypertable for time-series health score tracking.
/// </summary>
public class DeviceHealthScoreConfiguration : IEntityTypeConfiguration<DeviceHealthScore>
{
    public void Configure(EntityTypeBuilder<DeviceHealthScore> builder)
    {
        builder.ToTable("device_health_scores", tb =>
            tb.HasComment("TimescaleDB hypertable for device health scores"));

        // TimescaleDB requires primary key to include partitioning column (timestamp)
        builder.HasKey(h => new { h.Id, h.Timestamp });

        builder.Property(h => h.Id)
            .HasColumnName("id")
            .IsRequired();

        // DeviceId value object conversion
        builder.Property(h => h.DeviceId)
            .HasColumnName("device_id")
            .HasConversion(
                v => v.Value,
                v => new DeviceId(v))
            .IsRequired();

        builder.Property(h => h.TotalScore)
            .HasColumnName("total_score")
            .IsRequired();

        builder.Property(h => h.HeartbeatScore)
            .HasColumnName("heartbeat_score")
            .IsRequired();

        builder.Property(h => h.ReconciliationScore)
            .HasColumnName("reconciliation_score")
            .IsRequired();

        builder.Property(h => h.ResourceScore)
            .HasColumnName("resource_score")
            .IsRequired();

        // Timestamp is the time dimension for TimescaleDB hypertable
        builder.Property(h => h.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        // Indexes optimized for TimescaleDB hypertable
        builder.HasIndex(h => h.DeviceId)
            .HasDatabaseName("ix_device_health_scores_device_id");

        builder.HasIndex(h => h.Timestamp)
            .HasDatabaseName("ix_device_health_scores_timestamp")
            .IsDescending(); // Optimize for recent data queries

        builder.HasIndex(h => h.TotalScore)
            .HasDatabaseName("ix_device_health_scores_total_score");

        // Composite index for device health over time queries
        builder.HasIndex(h => new { h.DeviceId, h.Timestamp })
            .HasDatabaseName("ix_device_health_scores_device_timestamp")
            .IsDescending(false, true); // DeviceId ASC, Timestamp DESC
    }
}
