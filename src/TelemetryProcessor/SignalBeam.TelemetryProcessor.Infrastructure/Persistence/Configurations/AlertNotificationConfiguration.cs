using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for AlertNotification entity.
/// </summary>
public class AlertNotificationConfiguration : IEntityTypeConfiguration<AlertNotification>
{
    public void Configure(EntityTypeBuilder<AlertNotification> builder)
    {
        builder.ToTable("alert_notifications", tb =>
            tb.HasComment("Alert notification delivery records"));

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(n => n.AlertId)
            .HasColumnName("alert_id")
            .IsRequired();

        // Enum stored as string for readability in database
        builder.Property(n => n.Channel)
            .HasColumnName("channel")
            .HasMaxLength(50)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<NotificationChannel>(v))
            .IsRequired();

        builder.Property(n => n.Recipient)
            .HasColumnName("recipient")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(n => n.SentAt)
            .HasColumnName("sent_at")
            .IsRequired();

        builder.Property(n => n.Success)
            .HasColumnName("success")
            .IsRequired();

        builder.Property(n => n.Error)
            .HasColumnName("error")
            .HasColumnType("text");

        // Foreign key relationship to Alert
        builder.HasOne<Alert>()
            .WithMany()
            .HasForeignKey(n => n.AlertId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for notification queries
        builder.HasIndex(n => n.AlertId)
            .HasDatabaseName("ix_alert_notifications_alert_id");

        builder.HasIndex(n => n.SentAt)
            .HasDatabaseName("ix_alert_notifications_sent_at")
            .IsDescending(); // Recent notifications first

        builder.HasIndex(n => n.Success)
            .HasDatabaseName("ix_alert_notifications_success");
    }
}
