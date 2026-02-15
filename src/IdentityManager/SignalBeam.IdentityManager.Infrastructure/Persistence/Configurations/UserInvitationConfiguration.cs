using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.IdentityManager.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for UserInvitation entity.
/// </summary>
public class UserInvitationConfiguration : IEntityTypeConfiguration<UserInvitation>
{
    public void Configure(EntityTypeBuilder<UserInvitation> builder)
    {
        builder.ToTable("user_invitations");

        // Primary key
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasConversion(
                id => id.Value,
                value => new InvitationId(value))
            .HasColumnName("id");

        // TenantId
        builder.Property(i => i.TenantId)
            .HasConversion(
                id => id.Value,
                value => new TenantId(value))
            .HasColumnName("tenant_id")
            .IsRequired();

        // Email
        builder.Property(i => i.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        // Role
        builder.Property(i => i.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // Status
        builder.Property(i => i.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // InvitedByUserId
        builder.Property(i => i.InvitedByUserId)
            .HasConversion(
                id => id.Value,
                value => new UserId(value))
            .HasColumnName("invited_by_user_id")
            .IsRequired();

        // Token
        builder.Property(i => i.Token)
            .HasColumnName("token")
            .IsRequired();

        // Timestamps
        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(i => i.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(i => i.RespondedAt)
            .HasColumnName("responded_at");

        // Indexes
        builder.HasIndex(i => i.TenantId)
            .HasDatabaseName("ix_user_invitations_tenant_id");

        builder.HasIndex(i => i.Email)
            .HasDatabaseName("ix_user_invitations_email");

        builder.HasIndex(i => i.Token)
            .IsUnique()
            .HasDatabaseName("uq_user_invitations_token");

        builder.HasIndex(i => i.Status)
            .HasDatabaseName("ix_user_invitations_status");

        // Ignore domain events collection
        builder.Ignore(i => i.DomainEvents);
    }
}
