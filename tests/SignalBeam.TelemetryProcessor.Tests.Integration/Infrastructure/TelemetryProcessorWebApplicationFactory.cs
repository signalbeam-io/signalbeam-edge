using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NATS.Client.Core;
using SignalBeam.TelemetryProcessor.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SignalBeam.TelemetryProcessor.Tests.Integration.Infrastructure;

/// <summary>
/// WebApplicationFactory for testing TelemetryProcessor Host.
/// Configures test containers for PostgreSQL and provides test configuration.
/// </summary>
public class TelemetryProcessorWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly string _natsUrl = "nats://localhost:4222";

    public string ConnectionString => _postgresContainer.GetConnectionString();
    public string NatsUrl => _natsUrl;

    public TelemetryProcessorWebApplicationFactory()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("timescale/timescaledb:latest-pg16")
            .WithDatabase("signalbeam_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Remove the existing DbContext registration
            services.RemoveAll<DbContextOptions<TelemetryDbContext>>();
            services.RemoveAll<TelemetryDbContext>();

            // Add test database context
            services.AddDbContext<TelemetryDbContext>(options =>
            {
                options.UseNpgsql(_postgresContainer.GetConnectionString());
            });

            // Keep NATS connection as-is (assumes local NATS for integration tests)
            // For true isolation, you could mock the NATS connection here
        });

        builder.UseEnvironment("Testing");
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        // Apply migrations
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
        await context.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>
    /// Gets a scoped TelemetryDbContext for test assertions.
    /// </summary>
    public TelemetryDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
    }
}
