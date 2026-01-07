using Microsoft.EntityFrameworkCore;
using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.Entities;
using System.Reflection;

namespace SignalBeam.IdentityManager.Infrastructure.Persistence;

/// <summary>
/// DbContext for IdentityManager service managing tenants, users, and subscriptions.
/// </summary>
public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ignore domain event base types (not persisted as separate entities)
        modelBuilder.Ignore<DomainEvent>();

        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Set default schema for identity management
        modelBuilder.HasDefaultSchema("identity");
    }
}
