using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using SignalBeam.Shared.Infrastructure.Messaging;
using SignalBeam.TelemetryProcessor.Application.MessageHandlers;
using SignalBeam.TelemetryProcessor.Application.Repositories;
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
        services.Configure<Messaging.Options.NatsOptions>(
            configuration.GetSection(Messaging.Options.NatsOptions.SectionName));

        // Register DbContext with connection string
        // Try Aspire-injected connection string first, fallback to TelemetryDb
        var connectionString = configuration.GetConnectionString("signalbeam")
            ?? configuration.GetConnectionString("TelemetryDb")
            ?? throw new InvalidOperationException(
                "Database connection string not found. Expected 'signalbeam' (Aspire) or 'TelemetryDb'.");

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

        // Register repositories with their interfaces
        services.AddScoped<IDeviceMetricsRepository, DeviceMetricsRepository>();
        services.AddScoped<IDeviceHeartbeatRepository, DeviceHeartbeatRepository>();
        services.AddScoped<IMetricsAggregateRepository, MetricsAggregateRepository>();

        // Register metrics and alerting repositories
        services.AddScoped<IDeviceHealthScoreRepository, DeviceHealthScoreRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IAlertNotificationRepository, AlertNotificationRepository>();

        // Register NATS connection
        var natsUrl = configuration.GetSection(Messaging.Options.NatsOptions.SectionName)["Url"]
            ?? configuration.GetConnectionString("nats") // Try Aspire connection string
            ?? "nats://localhost:4222";

        // Normalize URL to use nats:// scheme (Aspire may provide tcp://)
        if (natsUrl.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
        {
            natsUrl = "nats://" + natsUrl.Substring("tcp://".Length);
        }
        else if (!natsUrl.StartsWith("nats://", StringComparison.OrdinalIgnoreCase))
        {
            natsUrl = "nats://" + natsUrl;
        }

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

        // Register as INatsConnection for IMessagePublisher
        services.AddSingleton<INatsConnection>(sp => sp.GetRequiredService<NatsConnection>());

        // Register JetStream context
        services.AddSingleton<INatsJSContext>(sp =>
        {
            var connection = sp.GetRequiredService<NatsConnection>();
            return new NatsJSContext(connection);
        });

        // Register message publisher for publishing events
        services.AddSingleton<IMessagePublisher, NatsMessagePublisher>();

        // Register application services
        services.AddScoped<SignalBeam.TelemetryProcessor.Application.Services.IDeviceHealthCalculator,
            SignalBeam.TelemetryProcessor.Application.Services.DeviceHealthCalculator>();

        // Configure alerting options
        services.Configure<SignalBeam.TelemetryProcessor.Application.Services.AlertRules.AlertingOptions>(
            configuration.GetSection(SignalBeam.TelemetryProcessor.Application.Services.AlertRules.AlertingOptions.SectionName));

        // Register alert rules
        services.AddScoped<SignalBeam.TelemetryProcessor.Application.Services.AlertRules.IAlertRule,
            SignalBeam.TelemetryProcessor.Application.Services.AlertRules.DeviceOfflineRule>();
        services.AddScoped<SignalBeam.TelemetryProcessor.Application.Services.AlertRules.IAlertRule,
            SignalBeam.TelemetryProcessor.Application.Services.AlertRules.DeviceUnhealthyRule>();
        services.AddScoped<SignalBeam.TelemetryProcessor.Application.Services.AlertRules.IAlertRule,
            SignalBeam.TelemetryProcessor.Application.Services.AlertRules.HighErrorRateRule>();

        // Configure notification options
        services.Configure<SignalBeam.TelemetryProcessor.Application.Services.Notifications.NotificationOptions>(
            configuration.GetSection(SignalBeam.TelemetryProcessor.Application.Services.Notifications.NotificationOptions.SectionName));

        // Register HttpClient for notification channels (Slack, Teams, etc.)
        services.AddHttpClient<SignalBeam.TelemetryProcessor.Infrastructure.Notifications.SlackNotificationChannel>();
        services.AddHttpClient<SignalBeam.TelemetryProcessor.Infrastructure.Notifications.TeamsNotificationChannel>();

        // Register notification channels
        services.AddScoped<SignalBeam.TelemetryProcessor.Application.Services.Notifications.INotificationChannel,
            SignalBeam.TelemetryProcessor.Infrastructure.Notifications.EmailNotificationChannel>();
        services.AddScoped<SignalBeam.TelemetryProcessor.Application.Services.Notifications.INotificationChannel,
            SignalBeam.TelemetryProcessor.Infrastructure.Notifications.SlackNotificationChannel>();
        services.AddScoped<SignalBeam.TelemetryProcessor.Application.Services.Notifications.INotificationChannel,
            SignalBeam.TelemetryProcessor.Infrastructure.Notifications.TeamsNotificationChannel>();

        // Register notification service
        services.AddScoped<SignalBeam.TelemetryProcessor.Application.Services.Notifications.IAlertNotificationService,
            SignalBeam.TelemetryProcessor.Infrastructure.Notifications.AlertNotificationService>();

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
        // Use the same connection string resolution as the main DbContext
        var connectionString = configuration.GetConnectionString("signalbeam")
            ?? configuration.GetConnectionString("TelemetryDb")
            ?? throw new InvalidOperationException(
                "Database connection string not found. Expected 'signalbeam' (Aspire) or 'TelemetryDb'.");

        services.AddHealthChecks()
            .AddNpgSql(
                connectionString,
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
