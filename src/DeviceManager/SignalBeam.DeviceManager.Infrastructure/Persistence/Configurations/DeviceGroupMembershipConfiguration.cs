using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Configurations;

public class DeviceGroupMembershipConfiguration : IEntityTypeConfiguration<DeviceGroupMembership>
{
    public void Configure(EntityTypeBuilder<DeviceGroupMembership> builder)
    {
        builder.ToTable("device_group_memberships");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("id")
            .HasConversion(
                v => v.Value,
                v => new DeviceGroupMembershipId(v))
            .IsRequired();

        // Foreign key to DeviceGroup
        builder.Property(m => m.GroupId)
            .HasColumnName("group_id")
            .HasConversion(
                v => v.Value,
                v => new DeviceGroupId(v))
            .IsRequired();

        // Foreign key to Device
        builder.Property(m => m.DeviceId)
            .HasColumnName("device_id")
            .HasConversion(
                v => v.Value,
                v => new DeviceId(v))
            .IsRequired();

        // MembershipType enum stored as string
        builder.Property(m => m.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.AddedAt)
            .HasColumnName("added_at")
            .IsRequired();

        builder.Property(m => m.AddedBy)
            .HasColumnName("added_by")
            .HasMaxLength(255)
            .IsRequired();

        // Indexes
        builder.HasIndex(m => m.GroupId)
            .HasDatabaseName("ix_device_group_memberships_group_id");

        builder.HasIndex(m => m.DeviceId)
            .HasDatabaseName("ix_device_group_memberships_device_id");

        // Unique constraint: a device can only be in a group once
        builder.HasIndex(m => new { m.GroupId, m.DeviceId })
            .HasDatabaseName("ix_device_group_memberships_group_device_unique")
            .IsUnique();

        // Ignore navigation properties (we're using a repository pattern, not EF navigation)
        // If you want navigation properties later, you can add them here
    }
}
