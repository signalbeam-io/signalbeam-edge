using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;
using SignalBeam.TelemetryProcessor.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SignalBeam.TelemetryProcessor.Tests.Integration.Infrastructure;

/// <summary>
/// Test fixture for TelemetryProcessor integration tests.
/// Manages PostgreSQL and NATS test containers.
/// </summary>
public class TelemetryProcessorTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private NatsConnection? _natsConnection;

    public string ConnectionString => _postgresContainer.GetConnectionString();
    public string NatsUrl { get; private set; } = "nats://localhost:4222";

    public TelemetryProcessorTestFixture()
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
        // Start PostgreSQL container
        await _postgresContainer.StartAsync();

        // Run migrations
        var services = new ServiceCollection();
        services.AddDbContext<TelemetryDbContext>(options =>
            options.UseNpgsql(ConnectionString));

        var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
        await context.Database.MigrateAsync();

        // Initialize NATS connection (assumes NATS is running locally or in CI)
        // For true isolation, you could use a NATS container, but NATS is lightweight enough to run locally
        try
        {
            var opts = new NatsOpts
            {
                Url = NatsUrl,
                ConnectTimeout = TimeSpan.FromSeconds(5)
            };
            _natsConnection = new NatsConnection(opts);
            await _natsConnection.ConnectAsync();
        }
        catch (Exception)
        {
            // If NATS is not available, tests will be skipped or use a mock
            _natsConnection = null;
        }
    }

    public async Task DisposeAsync()
    {
        if (_natsConnection != null)
        {
            await _natsConnection.DisposeAsync();
        }
        await _postgresContainer.DisposeAsync();
    }

    public TelemetryDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TelemetryDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new TelemetryDbContext(options);
    }

    public bool IsNatsAvailable() => _natsConnection?.ConnectionState == NatsConnectionState.Open;
}
