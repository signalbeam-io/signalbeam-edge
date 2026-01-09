using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.IdentityManager.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for User entity.
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        // Primary key
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasConversion(
                id => id.Value,
                value => new UserId(value))
            .HasColumnName("id");

        // TenantId
        builder.Property(u => u.TenantId)
            .HasConversion(
                id => id.Value,
                value => new TenantId(value))
            .HasColumnName("tenant_id")
            .IsRequired();

        // Email
        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        // Name
        builder.Property(u => u.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        // ZitadelUserId (external identity provider ID)
        builder.Property(u => u.ZitadelUserId)
            .HasColumnName("zitadel_user_id")
            .HasMaxLength(100)
            .IsRequired();

        // Role
        builder.Property(u => u.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // Status
        builder.Property(u => u.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // Timestamps
        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(u => u.LastLoginAt)
            .HasColumnName("last_login_at");

        // Indexes
        builder.HasIndex(u => u.TenantId)
            .HasDatabaseName("ix_users_tenant_id");

        builder.HasIndex(u => u.ZitadelUserId)
            .IsUnique()
            .HasDatabaseName("uq_users_zitadel_user_id");

        builder.HasIndex(u => u.Email)
            .HasDatabaseName("ix_users_email");

        builder.HasIndex(u => u.Status)
            .HasDatabaseName("ix_users_status");

        // Ignore domain events collection
        builder.Ignore(u => u.DomainEvents);
    }
}
