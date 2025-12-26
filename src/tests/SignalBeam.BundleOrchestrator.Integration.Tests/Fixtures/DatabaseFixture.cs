using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.BundleOrchestrator.Infrastructure.Persistence;
using SignalBeam.BundleOrchestrator.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace SignalBeam.BundleOrchestrator.Integration.Tests.Fixtures;

/// <summary>
/// Database fixture for integration tests using Testcontainers.
/// Spins up a PostgreSQL container and provides a configured DbContext.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private ServiceProvider? _serviceProvider;

    public DatabaseFixture()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("signalbeam_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
    }

    public string ConnectionString => _postgresContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        // Start the PostgreSQL container
        await _postgresContainer.StartAsync();

        // Configure services
        var services = new ServiceCollection();

        services.AddDbContext<BundleDbContext>(options =>
            options.UseNpgsql(ConnectionString));

        // Register repositories
        services.AddScoped<IRolloutRepository, RolloutRepository>();
        services.AddScoped<IBundleRepository, BundleRepository>();

        _serviceProvider = services.BuildServiceProvider();

        // Apply migrations and ensure database is created
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BundleDbContext>();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider != null)
        {
            await _serviceProvider.DisposeAsync();
        }

        await _postgresContainer.DisposeAsync();
    }

    /// <summary>
    /// Creates a new scope with fresh DbContext and repositories.
    /// </summary>
    public IServiceScope CreateScope()
    {
        if (_serviceProvider == null)
            throw new InvalidOperationException("DatabaseFixture not initialized");

        return _serviceProvider.CreateScope();
    }

    /// <summary>
    /// Cleans all data from the database between tests.
    /// </summary>
    public async Task CleanDatabaseAsync()
    {
        using var scope = CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BundleDbContext>();

        // Delete all data from tables (use simpler approach)
        context.RolloutDeviceAssignments.RemoveRange(context.RolloutDeviceAssignments);
        context.RolloutPhases.RemoveRange(context.RolloutPhases);
        context.Rollouts.RemoveRange(context.Rollouts);
        context.RolloutStatuses.RemoveRange(context.RolloutStatuses);
        context.DeviceDesiredStates.RemoveRange(context.DeviceDesiredStates);
        context.AppBundleVersions.RemoveRange(context.AppBundleVersions);
        context.AppBundles.RemoveRange(context.AppBundles);

        await context.SaveChangesAsync();
    }
}

/// <summary>
/// Collection fixture to share database across multiple test classes.
/// </summary>
[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}
