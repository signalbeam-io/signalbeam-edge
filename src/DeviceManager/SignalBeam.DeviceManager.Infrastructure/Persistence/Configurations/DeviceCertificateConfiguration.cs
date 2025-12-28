using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for DeviceCertificate entity (for mTLS).
/// </summary>
public class DeviceCertificateConfiguration : IEntityTypeConfiguration<DeviceCertificate>
{
    public void Configure(EntityTypeBuilder<DeviceCertificate> builder)
    {
        builder.ToTable("device_certificates");

        // Primary key
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .IsRequired();

        // DeviceId (foreign key)
        builder.Property(c => c.DeviceId)
            .HasConversion(
                id => id.Value,
                value => new DeviceId(value))
            .HasColumnName("device_id")
            .IsRequired();

        // Certificate data
        builder.Property(c => c.Certificate)
            .HasColumnName("certificate")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(c => c.SerialNumber)
            .HasColumnName("serial_number")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(c => c.Fingerprint)
            .HasColumnName("fingerprint")
            .HasMaxLength(64)
            .IsRequired();

        // Timestamps
        builder.Property(c => c.IssuedAt)
            .HasColumnName("issued_at")
            .IsRequired();

        builder.Property(c => c.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(c => c.RevokedAt)
            .HasColumnName("revoked_at");

        // Indexes
        builder.HasIndex(c => c.DeviceId)
            .HasDatabaseName("ix_device_certificates_device_id");

        builder.HasIndex(c => c.SerialNumber)
            .IsUnique()
            .HasDatabaseName("ix_device_certificates_serial_number");

        builder.HasIndex(c => c.Fingerprint)
            .HasDatabaseName("ix_device_certificates_fingerprint");
    }
}
