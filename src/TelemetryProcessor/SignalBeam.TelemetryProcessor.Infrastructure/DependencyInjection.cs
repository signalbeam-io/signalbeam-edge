using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using SignalBeam.TelemetryProcessor.Application.MessageHandlers;
using SignalBeam.TelemetryProcessor.Infrastructure.Messaging;
using SignalBeam.TelemetryProcessor.Infrastructure.Messaging.Options;
using SignalBeam.TelemetryProcessor.Infrastructure.Persistence;
using SignalBeam.TelemetryProcessor.Infrastructure.Persistence.Repositories;
using SignalBeam.TelemetryProcessor.Infrastructure.Resilience;

namespace SignalBeam.TelemetryProcessor.Infrastructure;

/// <summary>
/// Extension methods for registering TelemetryProcessor infrastructure services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds TelemetryProcessor infrastructure services to the service collection.
    /// </summary>
    public static IServiceCollection AddTelemetryProcessorInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure options
        services.Configure<NatsOptions>(configuration.GetSection(NatsOptions.SectionName));

        // Register DbContext with connection string
        var connectionString = configuration.GetConnectionString("TelemetryDb")
            ?? throw new InvalidOperationException("TelemetryDb connection string is not configured");

        services.AddDbContext<TelemetryDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);

                // Set command timeout for long-running queries
                npgsqlOptions.CommandTimeout(30);
            });

            // Enable detailed errors in development
            if (configuration.GetValue<bool>("DetailedErrors"))
            {
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
            }
        });

        // Register repositories
        services.AddScoped<DeviceMetricsRepository>();
        services.AddScoped<DeviceHeartbeatRepository>();

        // Register NATS connection
        var natsUrl = configuration.GetSection(NatsOptions.SectionName)["Url"]
            ?? "nats://localhost:4222";

        services.AddSingleton<NatsConnection>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<NatsConnection>>();
            var opts = new NatsOpts
            {
                Url = natsUrl,
                Name = "TelemetryProcessor",
                ConnectTimeout = TimeSpan.FromSeconds(5)
            };

            logger.LogInformation("Connecting to NATS at {NatsUrl}", natsUrl);
            return new NatsConnection(opts);
        });

        // Register JetStream context
        services.AddSingleton<INatsJSContext>(sp =>
        {
            var connection = sp.GetRequiredService<NatsConnection>();
            return new NatsJSContext(connection);
        });

        // Register message handlers from Application layer
        services.AddScoped<DeviceHeartbeatMessageHandler>();
        services.AddScoped<DeviceMetricsMessageHandler>();

        // Register NATS consumer as hosted service
        services.AddHostedService<NatsConsumerService>();

        // Resilience policies are created as static methods and called directly where needed

        return services;
    }

    /// <summary>
    /// Adds health checks for TelemetryProcessor infrastructure.
    /// </summary>
    public static IServiceCollection AddTelemetryProcessorHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddNpgSql(
                configuration.GetConnectionString("TelemetryDb")!,
                name: "telemetry-db",
                tags: new[] { "database", "postgresql", "timescaledb" })
            .AddCheck<NatsHealthCheck>(
                name: "nats",
                tags: new[] { "messaging", "nats" });

        return services;
    }
}

/// <summary>
/// Health check for NATS connection.
/// </summary>
public class NatsHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly NatsConnection _connection;

    public NatsHealthCheck(NatsConnection connection)
    {
        _connection = connection;
    }

    public Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isConnected = _connection.ConnectionState == NatsConnectionState.Open;

            if (isConnected)
            {
                return Task.FromResult(
                    Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("NATS connection is open"));
            }
            else
            {
                return Task.FromResult(
                    Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                        $"NATS connection is {_connection.ConnectionState}"));
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                    "Failed to check NATS connection", ex));
        }
    }
}
