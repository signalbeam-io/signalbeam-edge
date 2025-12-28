using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for DeviceAuthenticationLog entity.
/// </summary>
public class DeviceAuthenticationLogConfiguration : IEntityTypeConfiguration<DeviceAuthenticationLog>
{
    public void Configure(EntityTypeBuilder<DeviceAuthenticationLog> builder)
    {
        builder.ToTable("device_authentication_logs");

        // Primary key
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("id")
            .IsRequired();

        // DeviceId (nullable - may not be identifiable)
        builder.Property(l => l.DeviceId)
            .HasConversion(
                id => id == null ? null : (Guid?)id.Value,
                value => value == null ? null : new DeviceId(value.Value))
            .HasColumnName("device_id");

        // Request details
        builder.Property(l => l.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45); // IPv6 max length

        builder.Property(l => l.UserAgent)
            .HasColumnName("user_agent")
            .HasColumnType("text");

        // Authentication result
        builder.Property(l => l.Success)
            .HasColumnName("success")
            .IsRequired();

        builder.Property(l => l.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(255);

        builder.Property(l => l.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        builder.Property(l => l.ApiKeyPrefix)
            .HasColumnName("api_key_prefix")
            .HasMaxLength(10);

        // Indexes for querying
        builder.HasIndex(l => l.DeviceId)
            .HasDatabaseName("ix_device_authentication_logs_device_id");

        builder.HasIndex(l => l.Timestamp)
            .HasDatabaseName("ix_device_authentication_logs_timestamp");

        builder.HasIndex(l => new { l.Success, l.Timestamp })
            .HasDatabaseName("ix_device_authentication_logs_success_timestamp");
    }
}
