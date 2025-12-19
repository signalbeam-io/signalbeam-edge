using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.BundleOrchestrator.Infrastructure.Persistence;
using SignalBeam.BundleOrchestrator.Infrastructure.Persistence.Repositories;
using SignalBeam.BundleOrchestrator.Infrastructure.Storage;
using SignalBeam.Shared.Infrastructure.Messaging;
using SignalBeam.Shared.Infrastructure.Authentication;

namespace SignalBeam.BundleOrchestrator.Infrastructure;

/// <summary>
/// Dependency injection configuration for BundleOrchestrator Infrastructure layer.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register DbContext
        services.AddDbContext<BundleDbContext>(options =>
        {
            // Try Aspire-injected connection string first, fallback to BundleDb
            var connectionString = configuration.GetConnectionString("signalbeam")
                ?? configuration.GetConnectionString("BundleDb")
                ?? throw new InvalidOperationException(
                    "Database connection string not found. Expected 'signalbeam' (Aspire) or 'BundleDb'.");

            options.UseNpgsql(connectionString);
        });

        // Register repositories
        services.AddScoped<IBundleRepository, BundleRepository>();
        services.AddScoped<IBundleVersionRepository, BundleVersionRepository>();
        services.AddScoped<IDeviceDesiredStateRepository, DeviceDesiredStateRepository>();
        services.AddScoped<IRolloutStatusRepository, RolloutStatusRepository>();
        services.AddScoped<IDeviceGroupRepository, DeviceGroupRepository>();

        // Azure Blob Storage for bundle artifacts
        ConfigureBlobStorage(services, configuration);

        // NATS message publisher
        ConfigureNats(services, configuration);

        // API Key Authentication
        services.AddSingleton<IApiKeyValidator, ApiKeyValidator>();

        return services;
    }

    private static void ConfigureBlobStorage(
        IServiceCollection services,
        IConfiguration configuration)
    {
        // Check if BlobServiceClient is already registered (by Aspire)
        var serviceDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(BlobServiceClient));

        if (serviceDescriptor == null)
        {
            // BlobServiceClient not registered by Aspire, configure it manually
            var useManagedIdentity = configuration.GetValue<bool>("AzureBlobStorage:UseManagedIdentity");
            var blobServiceUri = configuration.GetValue<string>("AzureBlobStorage:ServiceUri");
            var connectionString = configuration.GetValue<string>("AzureBlobStorage:ConnectionString");

            if (useManagedIdentity && !string.IsNullOrEmpty(blobServiceUri))
            {
                // Use Managed Identity for authentication (production)
                services.AddSingleton(sp =>
                {
                    var credential = new DefaultAzureCredential();
                    return new BlobServiceClient(new Uri(blobServiceUri), credential);
                });
            }
            else if (!string.IsNullOrEmpty(connectionString))
            {
                // Use connection string (development)
                services.AddSingleton(sp => new BlobServiceClient(connectionString));
            }
            else
            {
                // No blob storage configured - throw an error
                services.AddSingleton<BlobServiceClient>(sp =>
                {
                    throw new InvalidOperationException(
                        "Azure Blob Storage is not configured. " +
                        "Set AzureBlobStorage:ConnectionString or AzureBlobStorage:ServiceUri with UseManagedIdentity=true");
                });
            }
        }
        // If BlobServiceClient is already registered (by Aspire), use it as-is

        // Register bundle storage service
        services.AddSingleton<IBundleStorageService>(sp =>
        {
            var blobClient = sp.GetRequiredService<BlobServiceClient>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BundleStorageService>>();
            var containerName = configuration.GetValue<string>("AzureBlobStorage:ContainerName") ?? "bundle-manifests";
            return new BundleStorageService(blobClient, logger, containerName);
        });
    }

    private static void ConfigureNats(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var natsUrl = configuration.GetValue<string>("NATS:Url") ?? "nats://localhost:4222";

        services.AddSingleton<INatsConnection>(sp =>
        {
            var opts = NatsOpts.Default with { Url = natsUrl };
            return new NatsConnection(opts);
        });

        services.AddSingleton<IMessagePublisher, NatsMessagePublisher>();
    }
}
