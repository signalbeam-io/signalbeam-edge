using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SignalBeam.IdentityManager.Infrastructure.Persistence;

/// <summary>
/// Design-time DbContext factory for EF Core migrations.
/// </summary>
public class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();

        // Use a default connection string for migrations
        // This will be overridden at runtime with the actual connection string
        optionsBuilder.UseNpgsql("Host=localhost;Database=signalbeam;Username=postgres;Password=postgres");

        return new IdentityDbContext(optionsBuilder.Options);
    }
}
