using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.EdgeAgent.Infrastructure.BackgroundServices;
using SignalBeam.EdgeAgent.Infrastructure.Cloud;
using SignalBeam.EdgeAgent.Infrastructure.Container;
using SignalBeam.EdgeAgent.Infrastructure.Metrics;
using SignalBeam.EdgeAgent.Infrastructure.Storage;
using SignalBeam.Shared.Infrastructure.Messaging;

namespace SignalBeam.EdgeAgent.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register Docker container manager
        services.AddSingleton<IContainerManager, DockerContainerManager>();

        // Register metrics collector
        services.AddSingleton<IMetricsCollector, SystemMetricsCollector>();

        // Register device credentials store
        services.AddSingleton<IDeviceCredentialsStore, FileDeviceCredentialsStore>();

        // Register API key handler
        services.AddTransient<DeviceApiKeyHandler>();

        // Register HTTP cloud client with mTLS and API key support
        services.AddHttpClient<ICloudClient, HttpCloudClient>((serviceProvider, client) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var cloudUrl = configuration["Agent:CloudUrl"] ?? "https://api.signalbeam.com";
            client.BaseAddress = new Uri(cloudUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
        {
            var handler = new HttpClientHandler();
            var credentialsStore = serviceProvider.GetRequiredService<IDeviceCredentialsStore>();

            // Load credentials synchronously (blocking during startup is acceptable)
            var credentials = credentialsStore.LoadCredentialsAsync().GetAwaiter().GetResult();

            // Configure client certificate if available
            if (credentials?.ClientCertificatePath != null &&
                credentials.ClientPrivateKeyPath != null &&
                File.Exists(credentials.ClientCertificatePath) &&
                File.Exists(credentials.ClientPrivateKeyPath))
            {
                try
                {
                    var certPem = File.ReadAllText(credentials.ClientCertificatePath);
                    var keyPem = File.ReadAllText(credentials.ClientPrivateKeyPath);
                    var clientCert = System.Security.Cryptography.X509Certificates.X509Certificate2
                        .CreateFromPem(certPem, keyPem);

                    handler.ClientCertificates.Add(clientCert);
                }
                catch (Exception ex)
                {
                    // Log warning but continue - will fall back to API key auth
                    Console.WriteLine($"Warning: Failed to load client certificate: {ex.Message}");
                }
            }

            // Configure CA certificate for server validation (optional)
            if (credentials?.CaCertificatePath != null && File.Exists(credentials.CaCertificatePath))
            {
                try
                {
                    var caCertPem = File.ReadAllText(credentials.CaCertificatePath);
                    var caCert = System.Security.Cryptography.X509Certificates.X509Certificate2
                        .CreateFromPem(caCertPem);

                    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                    {
                        // Add CA cert to chain for validation
                        chain?.ChainPolicy.ExtraStore.Add(caCert);
                        return errors == System.Net.Security.SslPolicyErrors.None;
                    };
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to load CA certificate: {ex.Message}");
                }
            }

            return handler;
        })
        .AddHttpMessageHandler<DeviceApiKeyHandler>(); // Keep API key as fallback

        // Register NATS connection
        var natsUrl = configuration.GetSection("NATS")["Url"]
            ?? configuration.GetConnectionString("nats") // Aspire connection string
            ?? "nats://localhost:4222";

        // Normalize URL scheme (Aspire may provide tcp://)
        if (natsUrl.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
        {
            natsUrl = "nats://" + natsUrl["tcp://".Length..];
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
                Name = "EdgeAgent",
                ConnectTimeout = TimeSpan.FromSeconds(10),
                MaxReconnectRetry = -1, // Unlimited reconnect attempts
                ReconnectWaitMin = TimeSpan.FromSeconds(2),
                ReconnectWaitMax = TimeSpan.FromSeconds(30), // Exponential backoff cap
                ReconnectJitter = TimeSpan.FromMilliseconds(500)
            };

            logger.LogInformation("Connecting to NATS at {NatsUrl}", natsUrl);
            return new NatsConnection(opts);
        });

        // Register as INatsConnection
        services.AddSingleton<INatsConnection>(sp => sp.GetRequiredService<NatsConnection>());

        // Register JetStream context
        services.AddSingleton<INatsJSContext>(sp =>
        {
            var connection = sp.GetRequiredService<NatsConnection>();
            return new NatsJSContext(connection);
        });

        // Register message publisher
        services.AddSingleton<IMessagePublisher, NatsMessagePublisher>();

        // Named HTTP client for certificate renewal operations (with API key auth)
        services.AddHttpClient("CloudClient", (serviceProvider, client) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var cloudUrl = configuration["Agent:CloudUrl"] ?? "https://api.signalbeam.com";
            client.BaseAddress = new Uri(cloudUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddHttpMessageHandler<DeviceApiKeyHandler>();

        return services;
    }
}
