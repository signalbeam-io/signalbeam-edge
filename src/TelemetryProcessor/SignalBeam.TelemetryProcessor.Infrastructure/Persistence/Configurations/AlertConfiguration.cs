using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Alert entity.
/// </summary>
public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("alerts", tb =>
            tb.HasComment("System alerts for monitoring and notifications"));

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .IsRequired();

        // TenantId value object conversion
        builder.Property(a => a.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(
                v => v.Value,
                v => new TenantId(v))
            .IsRequired();

        // Enum stored as string for readability in database
        builder.Property(a => a.Severity)
            .HasColumnName("severity")
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<AlertSeverity>(v))
            .IsRequired();

        builder.Property(a => a.Type)
            .HasColumnName("type")
            .HasMaxLength(50)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<AlertType>(v))
            .IsRequired();

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<AlertStatus>(v))
            .HasDefaultValue(AlertStatus.Active)
            .IsRequired();

        builder.Property(a => a.Title)
            .HasColumnName("title")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(a => a.Description)
            .HasColumnName("description")
            .HasColumnType("text")
            .IsRequired();

        // DeviceId value object conversion (nullable)
        builder.Property(a => a.DeviceId)
            .HasColumnName("device_id")
            .HasConversion(
                v => v!.Value,
                v => new DeviceId(v));

        builder.Property(a => a.RolloutId)
            .HasColumnName("rollout_id");

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(a => a.AcknowledgedAt)
            .HasColumnName("acknowledged_at");

        builder.Property(a => a.AcknowledgedBy)
            .HasColumnName("acknowledged_by")
            .HasMaxLength(255);

        builder.Property(a => a.ResolvedAt)
            .HasColumnName("resolved_at");

        // Indexes for alert queries
        builder.HasIndex(a => a.TenantId)
            .HasDatabaseName("ix_alerts_tenant_id");

        builder.HasIndex(a => a.Status)
            .HasDatabaseName("ix_alerts_status");

        builder.HasIndex(a => a.CreatedAt)
            .HasDatabaseName("ix_alerts_created_at")
            .IsDescending(); // Recent alerts first

        builder.HasIndex(a => a.DeviceId)
            .HasDatabaseName("ix_alerts_device_id")
            .HasFilter("device_id IS NOT NULL");

        builder.HasIndex(a => new { a.Type, a.Severity })
            .HasDatabaseName("ix_alerts_type_severity");

        // Composite index for finding active alerts by device and type (deduplication)
        builder.HasIndex(a => new { a.DeviceId, a.Type, a.Status })
            .HasDatabaseName("ix_alerts_device_type_status")
            .HasFilter("device_id IS NOT NULL AND status = 'Active'");
    }
}
