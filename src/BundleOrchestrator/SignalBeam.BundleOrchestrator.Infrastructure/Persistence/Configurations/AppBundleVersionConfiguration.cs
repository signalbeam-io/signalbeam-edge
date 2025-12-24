using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using System.Text.Json;

namespace SignalBeam.BundleOrchestrator.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for AppBundleVersion entity.
/// </summary>
public class AppBundleVersionConfiguration : IEntityTypeConfiguration<AppBundleVersion>
{
    public void Configure(EntityTypeBuilder<AppBundleVersion> builder)
    {
        builder.ToTable("app_bundle_versions");

        // Primary key
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id)
            .HasColumnName("id");

        // BundleId
        builder.Property(v => v.BundleId)
            .HasConversion(
                id => id.Value,
                value => new BundleId(value))
            .HasColumnName("bundle_id")
            .IsRequired();

        // Version - stored as string
        builder.Property(v => v.Version)
            .HasConversion(
                v => v.ToString(),
                s => BundleVersion.Parse(s))
            .HasColumnName("version")
            .HasMaxLength(50)
            .IsRequired();

        // Containers - stored as JSONB
        builder.Property(v => v.Containers)
            .HasConversion(
                containers => JsonSerializer.Serialize(containers, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<List<ContainerSpec>>(json, (JsonSerializerOptions?)null) ?? new List<ContainerSpec>())
            .HasColumnName("containers")
            .HasColumnType("jsonb")
            .IsRequired();

        // CreatedAt
        builder.Property(v => v.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // ReleaseNotes
        builder.Property(v => v.ReleaseNotes)
            .HasColumnName("release_notes")
            .HasColumnType("text");

        // BlobStorageUri
        builder.Property(v => v.BlobStorageUri)
            .HasColumnName("blob_storage_uri")
            .HasColumnType("text");

        // Checksum
        builder.Property(v => v.Checksum)
            .HasColumnName("checksum")
            .HasMaxLength(80);

        // SizeBytes
        builder.Property(v => v.SizeBytes)
            .HasColumnName("size_bytes");

        // Status
        builder.Property(v => v.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();

        // Indexes for performance
        builder.HasIndex(v => v.BundleId)
            .HasDatabaseName("ix_app_bundle_versions_bundle_id");

        builder.HasIndex(v => new { v.BundleId, v.Version })
            .IsUnique()
            .HasDatabaseName("ix_app_bundle_versions_bundle_id_version");

        builder.HasIndex(v => v.CreatedAt)
            .HasDatabaseName("ix_app_bundle_versions_created_at");

        builder.HasIndex(v => v.Status)
            .HasDatabaseName("ix_app_bundle_versions_status");
    }
}
