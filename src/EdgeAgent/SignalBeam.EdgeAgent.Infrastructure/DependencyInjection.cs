using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.EdgeAgent.Infrastructure.Cloud;
using SignalBeam.EdgeAgent.Infrastructure.Container;
using SignalBeam.EdgeAgent.Infrastructure.Metrics;
using SignalBeam.EdgeAgent.Infrastructure.Storage;

namespace SignalBeam.EdgeAgent.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
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

        return services;
    }
}
