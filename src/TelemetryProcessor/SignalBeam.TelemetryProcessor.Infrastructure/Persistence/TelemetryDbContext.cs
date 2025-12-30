using Microsoft.EntityFrameworkCore;
using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.Entities;
using System.Reflection;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Persistence;

/// <summary>
/// DbContext for TelemetryProcessor service.
/// Handles time-series data storage with TimescaleDB optimizations.
/// </summary>
public class TelemetryDbContext : DbContext
{
    public TelemetryDbContext(DbContextOptions<TelemetryDbContext> options) : base(options)
    {
    }

    // TimescaleDB hypertables (time-series data)
    public DbSet<DeviceHeartbeat> DeviceHeartbeats => Set<DeviceHeartbeat>();
    public DbSet<DeviceMetrics> DeviceMetrics => Set<DeviceMetrics>();
    public DbSet<DeviceHealthScore> DeviceHealthScores => Set<DeviceHealthScore>();

    // Alerting system
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<AlertNotification> AlertNotifications => Set<AlertNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ignore domain event base types (not persisted as separate entities)
        modelBuilder.Ignore<DomainEvent>();

        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Set default schema for telemetry processor
        modelBuilder.HasDefaultSchema("telemetry_processor");
    }
}
