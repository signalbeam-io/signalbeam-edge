using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for AppBundle entity.
/// </summary>
public class AppBundleConfiguration : IEntityTypeConfiguration<AppBundle>
{
    public void Configure(EntityTypeBuilder<AppBundle> builder)
    {
        builder.ToTable("app_bundles");

        // Primary key
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasConversion(
                id => id.Value,
                value => new BundleId(value))
            .HasColumnName("id");

        // TenantId
        builder.Property(b => b.TenantId)
            .HasConversion(
                id => id.Value,
                value => new TenantId(value))
            .HasColumnName("tenant_id")
            .IsRequired();

        // Name
        builder.Property(b => b.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        // Description
        builder.Property(b => b.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        // CreatedAt
        builder.Property(b => b.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // LatestVersion - stored as string
        builder.Property(b => b.LatestVersion)
            .HasConversion(
                v => v == null ? null : v.ToString(),
                s => s == null ? null : BundleVersion.Parse(s))
            .HasColumnName("latest_version")
            .HasMaxLength(50);

        // Indexes for performance
        builder.HasIndex(b => b.TenantId)
            .HasDatabaseName("ix_app_bundles_tenant_id");

        builder.HasIndex(b => new { b.TenantId, b.Name })
            .IsUnique()
            .HasDatabaseName("ix_app_bundles_tenant_id_name");

        // Ignore domain events collection (not persisted)
        builder.Ignore("DomainEvents");
    }
}
