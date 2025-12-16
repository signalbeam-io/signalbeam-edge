using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for DeviceHeartbeat entity.
/// Configures as TimescaleDB hypertable for time-series data.
/// </summary>
public class DeviceHeartbeatConfiguration : IEntityTypeConfiguration<DeviceHeartbeat>
{
    public void Configure(EntityTypeBuilder<DeviceHeartbeat> builder)
    {
        builder.ToTable("device_heartbeats", tb =>
            tb.HasComment("TimescaleDB hypertable for device heartbeats"));

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

        // Timestamp is the time dimension for TimescaleDB hypertable
        builder.Property(h => h.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        builder.Property(h => h.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(h => h.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45); // IPv6 max length

        builder.Property(h => h.AdditionalData)
            .HasColumnName("additional_data")
            .HasColumnType("jsonb");

        // Indexes for TimescaleDB hypertable
        // TimescaleDB automatically creates index on (time, space) but we add explicit ones
        builder.HasIndex(h => h.DeviceId)
            .HasDatabaseName("ix_device_heartbeats_device_id");

        builder.HasIndex(h => h.Timestamp)
            .HasDatabaseName("ix_device_heartbeats_timestamp")
            .IsDescending(); // Optimize for recent data queries

        builder.HasIndex(h => new { h.DeviceId, h.Timestamp })
            .HasDatabaseName("ix_device_heartbeats_device_timestamp")
            .IsDescending(false, true); // DeviceId ASC, Timestamp DESC
    }
}
