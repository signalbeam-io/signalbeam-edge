using Microsoft.EntityFrameworkCore;
using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.Entities;
using System.Reflection;

namespace SignalBeam.DeviceManager.Infrastructure.Persistence;

/// <summary>
/// DbContext for DeviceManager service.
/// </summary>
public class DeviceDbContext : DbContext
{
    public DeviceDbContext(DbContextOptions<DeviceDbContext> options) : base(options)
    {
    }

    // Regular tables
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceGroup> DeviceGroups => Set<DeviceGroup>();
    public DbSet<DeviceGroupMembership> DeviceGroupMemberships => Set<DeviceGroupMembership>();
    public DbSet<DeviceActivityLog> DeviceActivityLogs => Set<DeviceActivityLog>();
    public DbSet<DeviceApiKey> DeviceApiKeys => Set<DeviceApiKey>();
    public DbSet<DeviceAuthenticationLog> DeviceAuthenticationLogs => Set<DeviceAuthenticationLog>();
    public DbSet<DeviceCertificate> DeviceCertificates => Set<DeviceCertificate>();
    public DbSet<DeviceRegistrationToken> DeviceRegistrationTokens => Set<DeviceRegistrationToken>();

    // TimescaleDB hypertables (time-series data)
    public DbSet<DeviceHeartbeat> DeviceHeartbeats => Set<DeviceHeartbeat>();
    public DbSet<DeviceMetrics> DeviceMetrics => Set<DeviceMetrics>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ignore domain event base types (not persisted as separate entities)
        modelBuilder.Ignore<DomainEvent>();

        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Set default schema
        modelBuilder.HasDefaultSchema("device_manager");
    }
}
