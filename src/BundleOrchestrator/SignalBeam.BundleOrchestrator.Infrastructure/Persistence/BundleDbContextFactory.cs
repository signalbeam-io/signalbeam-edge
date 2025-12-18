using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SignalBeam.BundleOrchestrator.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for BundleDbContext.
/// Used by EF Core tools for migrations.
/// </summary>
public class BundleDbContextFactory : IDesignTimeDbContextFactory<BundleDbContext>
{
    public BundleDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BundleDbContext>();

        // Use a default connection string for migrations
        // This will be overridden at runtime by the actual configuration
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=signalbeam;Username=postgres;Password=postgres",
            b => b.MigrationsHistoryTable("__EFMigrationsHistory", "bundle_orchestrator"));

        return new BundleDbContext(optionsBuilder.Options);
    }
}
