using Microsoft.EntityFrameworkCore;
using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.Entities;
using System.Reflection;

namespace SignalBeam.BundleOrchestrator.Infrastructure.Persistence;

/// <summary>
/// DbContext for BundleOrchestrator service.
/// </summary>
public class BundleDbContext : DbContext
{
    public BundleDbContext(DbContextOptions<BundleDbContext> options) : base(options)
    {
    }

    // Bundle management tables
    public DbSet<AppBundle> AppBundles => Set<AppBundle>();
    public DbSet<AppBundleVersion> AppBundleVersions => Set<AppBundleVersion>();
    public DbSet<DeviceDesiredState> DeviceDesiredStates => Set<DeviceDesiredState>();
    public DbSet<RolloutStatus> RolloutStatuses => Set<RolloutStatus>();

    // Rollout management tables
    public DbSet<Rollout> Rollouts => Set<Rollout>();
    public DbSet<RolloutPhase> RolloutPhases => Set<RolloutPhase>();
    public DbSet<RolloutDeviceAssignment> RolloutDeviceAssignments => Set<RolloutDeviceAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ignore domain event base types (not persisted as separate entities)
        modelBuilder.Ignore<DomainEvent>();

        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Set default schema
        modelBuilder.HasDefaultSchema("bundle_orchestrator");
    }
}
