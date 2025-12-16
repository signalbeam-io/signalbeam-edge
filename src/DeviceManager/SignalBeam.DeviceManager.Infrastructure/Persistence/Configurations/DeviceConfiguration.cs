using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using System.Text.Json;

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Device entity.
/// </summary>
public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices");

        // Primary key
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasConversion(
                id => id.Value,
                value => new DeviceId(value))
            .HasColumnName("id");

        // TenantId
        builder.Property(d => d.TenantId)
            .HasConversion(
                id => id.Value,
                value => new TenantId(value))
            .HasColumnName("tenant_id")
            .IsRequired();

        // Name
        builder.Property(d => d.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        // Status
        builder.Property(d => d.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // Timestamps
        builder.Property(d => d.LastSeenAt)
            .HasColumnName("last_seen_at");

        builder.Property(d => d.RegisteredAt)
            .HasColumnName("registered_at")
            .IsRequired();

        // Metadata (JSON)
        builder.Property(d => d.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        // Tags (stored as JSON array in PostgreSQL)
        builder.Property<List<string>>("_tags")
            .HasColumnName("tags")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(
                new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

        // AssignedBundleId
        builder.Property(d => d.AssignedBundleId)
            .HasConversion(
                id => id == null ? null : (Guid?)id.Value,
                value => value == null ? null : new BundleId(value.Value))
            .HasColumnName("assigned_bundle_id");

        // BundleDeploymentStatus
        builder.Property(d => d.BundleDeploymentStatus)
            .HasColumnName("bundle_deployment_status")
            .HasConversion<string?>()
            .HasMaxLength(50);

        // DeviceGroupId
        builder.Property(d => d.DeviceGroupId)
            .HasConversion(
                id => id == null ? null : (Guid?)id.Value,
                value => value == null ? null : new DeviceGroupId(value.Value))
            .HasColumnName("device_group_id");

        // Indexes for performance
        builder.HasIndex(d => d.TenantId)
            .HasDatabaseName("ix_devices_tenant_id");

        builder.HasIndex(d => d.Status)
            .HasDatabaseName("ix_devices_status");

        builder.HasIndex(d => d.LastSeenAt)
            .HasDatabaseName("ix_devices_last_seen_at");

        // Ignore domain events collection (not persisted)
        builder.Ignore("DomainEvents");
    }
}
