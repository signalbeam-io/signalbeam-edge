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

        // Register HTTP cloud client with API key handler
        services.AddHttpClient<ICloudClient, HttpCloudClient>((serviceProvider, client) =>
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
