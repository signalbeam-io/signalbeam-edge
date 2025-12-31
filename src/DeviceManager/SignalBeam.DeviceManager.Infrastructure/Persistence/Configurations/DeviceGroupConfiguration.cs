using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Configurations;

public class DeviceGroupConfiguration : IEntityTypeConfiguration<DeviceGroup>
{
    public void Configure(EntityTypeBuilder<DeviceGroup> builder)
    {
        builder.ToTable("device_groups");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id)
            .HasColumnName("id")
            .HasConversion(
                v => v.Value,
                v => new DeviceGroupId(v))
            .IsRequired();

        // TenantId value object conversion
        builder.Property(g => g.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(
                v => v.Value,
                v => new TenantId(v))
            .IsRequired();

        builder.Property(g => g.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(g => g.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        // GroupType enum stored as string
        builder.Property(g => g.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Tag query for dynamic groups
        builder.Property(g => g.TagQuery)
            .HasColumnName("tag_query")
            .HasMaxLength(1000);

        // Store the private _tagCriteria backing field (DEPRECATED - kept for backward compatibility)
        builder.Property<List<string>>("_tagCriteria")
            .HasColumnName("tag_criteria")
            .HasColumnType("jsonb")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(
                new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

        builder.Property(g => g.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Indexes
        builder.HasIndex(g => g.TenantId)
            .HasDatabaseName("ix_device_groups_tenant_id");

        builder.HasIndex(g => new { g.TenantId, g.Name })
            .HasDatabaseName("ix_device_groups_tenant_name")
            .IsUnique();
    }
}
