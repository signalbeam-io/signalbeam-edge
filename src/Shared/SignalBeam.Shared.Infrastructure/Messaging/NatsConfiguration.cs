using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;

namespace SignalBeam.Shared.Infrastructure.Messaging;

/// <summary>
/// Extension methods for configuring NATS client.
/// </summary>
public static class NatsConfiguration
{
    /// <summary>
    /// Adds NATS client with default configuration.
    /// </summary>
    public static IServiceCollection AddNatsClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var natsUrl = configuration["NATS:Url"] ?? "nats://localhost:4222";

        services.AddSingleton<INatsConnection>(_ =>
        {
            var options = new NatsOpts
            {
                Url = natsUrl,
                Name = "SignalBeam"
            };

            return new NatsConnection(options);
        });

        services.AddSingleton<IMessagePublisher, NatsMessagePublisher>();

        return services;
    }

    /// <summary>
    /// Adds NATS client with custom options.
    /// </summary>
    public static IServiceCollection AddNatsClient(
        this IServiceCollection services,
        Action<NatsOpts> configureOptions)
    {
        services.AddSingleton<INatsConnection>(serviceProvider =>
        {
            var options = new NatsOpts();
            configureOptions(options);
            return new NatsConnection(options);
        });

        services.AddSingleton<IMessagePublisher, NatsMessagePublisher>();

        return services;
    }
}

/// <summary>
/// NATS configuration options.
/// </summary>
public class NatsOptions
{
    /// <summary>
    /// NATS server URL (e.g., nats://localhost:4222).
    /// </summary>
    public string Url { get; set; } = "nats://localhost:4222";

    /// <summary>
    /// Client name for identification.
    /// </summary>
    public string Name { get; set; } = "SignalBeam";

    /// <summary>
    /// Maximum reconnect attempts.
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 10;

    /// <summary>
    /// Reconnect wait time in milliseconds.
    /// </summary>
    public int ReconnectWaitMs { get; set; } = 2000;
}
