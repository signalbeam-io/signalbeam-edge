using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SignalBeam.DeviceManager.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SignalBeam.DeviceManager.Tests.Integration.Infrastructure;

public class DeviceManagerTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    public string ConnectionString => _postgresContainer.GetConnectionString();

    public DeviceManagerTestFixture()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("timescale/timescaledb:latest-pg16")
            .WithDatabase("signalbeam_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        // Run migrations
        var services = new ServiceCollection();
        services.AddDbContext<DeviceDbContext>(options =>
            options.UseNpgsql(ConnectionString));

        var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DeviceDbContext>();

        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
    }

    public DeviceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DeviceDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new DeviceDbContext(options);
    }
}
