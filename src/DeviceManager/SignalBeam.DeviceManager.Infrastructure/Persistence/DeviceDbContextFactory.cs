using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SignalBeam.DeviceManager.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for creating DeviceDbContext instances for EF Core migrations.
/// </summary>
public class DeviceDbContextFactory : IDesignTimeDbContextFactory<DeviceDbContext>
{
    public DeviceDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DeviceDbContext>();

        // Use a default connection string for migrations
        // In production, this comes from configuration
        var connectionString = "Host=localhost;Database=signalbeam_dev;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "device_manager");
        });

        return new DeviceDbContext(optionsBuilder.Options);
    }
}
