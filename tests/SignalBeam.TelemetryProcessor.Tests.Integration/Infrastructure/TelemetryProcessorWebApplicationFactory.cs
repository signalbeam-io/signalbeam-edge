using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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
        _postgresContainer = new PostgreSqlBuilder("timescale/timescaledb:latest-pg16")
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

            // Replace the dependency health checks (NATS broker + external Postgres) with a single
            // self check. This harness has no NATS broker, and NatsConnection connects lazily so its
            // state would be non-deterministic; the database path is exercised directly by the
            // repository integration tests. Clearing then re-adding keeps /health deterministic.
            services.Configure<HealthCheckServiceOptions>(options => options.Registrations.Clear());
            services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live", "ready" });
        });

        builder.UseEnvironment("Testing");
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        // Build the schema from the model (the Infrastructure project ships no migration files, so
        // MigrateAsync() would be a no-op leaving the tables missing).
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
        await context.Database.EnsureCreatedAsync();
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
