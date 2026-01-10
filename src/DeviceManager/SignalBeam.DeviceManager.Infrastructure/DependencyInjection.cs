using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.DeviceManager.Infrastructure.Authentication;
using SignalBeam.DeviceManager.Infrastructure.BackgroundServices;
using SignalBeam.DeviceManager.Infrastructure.Caching;
using SignalBeam.DeviceManager.Infrastructure.Persistence;
using SignalBeam.DeviceManager.Infrastructure.Persistence.Repositories;
using SignalBeam.DeviceManager.Infrastructure.Storage;
using SignalBeam.Shared.Infrastructure.Authentication;
using SignalBeam.Shared.Infrastructure.Http;
using SignalBeam.Shared.Infrastructure.Messaging;
using StackExchange.Redis;

namespace SignalBeam.DeviceManager.Infrastructure;

/// <summary>
/// Dependency injection configuration for Infrastructure layer.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register DbContext
        services.AddDbContext<DeviceDbContext>(options =>
        {
            // Try Aspire-injected connection string first, fallback to DeviceDb
            var connectionString = configuration.GetConnectionString("signalbeam")
                ?? configuration.GetConnectionString("DeviceDb")
                ?? throw new InvalidOperationException("Database connection string not found. Expected 'signalbeam' (Aspire) or 'DeviceDb'.");

            options.UseNpgsql(connectionString);
        });

        // Register repositories
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IDeviceQueryRepository, DeviceRepository>();
        services.AddScoped<IDeviceMetricsRepository, DeviceMetricsRepository>();
        services.AddScoped<IDeviceMetricsQueryRepository, DeviceMetricsRepository>();
        services.AddScoped<IDeviceActivityLogRepository, DeviceActivityLogRepository>();
        services.AddScoped<IDeviceActivityLogQueryRepository, DeviceActivityLogRepository>();
        services.AddScoped<IDeviceGroupRepository, DeviceGroupRepository>();
        services.AddScoped<IDeviceGroupMembershipRepository, DeviceGroupMembershipRepository>();
        services.AddScoped<IDeviceHeartbeatRepository, DeviceHeartbeatRepository>();
        services.AddScoped<IDeviceApiKeyRepository, DeviceApiKeyRepository>();
        services.AddScoped<IDeviceAuthenticationLogRepository, DeviceAuthenticationLogRepository>();
        services.AddScoped<IDeviceRegistrationTokenRepository, DeviceRegistrationTokenRepository>();
        services.AddScoped<IDeviceCertificateRepository, DeviceCertificateRepository>();

        // Register application services
        services.AddScoped<SignalBeam.DeviceManager.Application.Services.IDynamicGroupMembershipManager,
            SignalBeam.DeviceManager.Application.Services.DynamicGroupMembershipManager>();

        // Register caching services
        services.AddMemoryCache();
        services.AddSingleton<TagQueryCache>();

        // Register HTTP context services
        services.AddHttpContextAccessor();
        services.AddScoped<IHttpContextInfoProvider, HttpContextInfoProvider>();

        // Register authentication services
        services.AddSingleton<IApiKeyValidator, ApiKeyValidator>();
        services.AddSingleton<IDeviceApiKeyService, DeviceApiKeyService>();
        services.AddSingleton<IRegistrationTokenService, RegistrationTokenService>();
        services.AddScoped<IDeviceApiKeyValidator, DeviceApiKeyValidator>();

        // NATS message publisher
        var natsUrl = configuration.GetValue<string>("NATS:Url") ?? "nats://localhost:4222";
        services.AddSingleton<INatsConnection>(sp =>
        {
            var opts = NatsOpts.Default with { Url = natsUrl };
            return new NatsConnection(opts);
        });
        services.AddSingleton<IMessagePublisher, NatsMessagePublisher>();

        // Azure Blob Storage client
        var blobConnectionString = configuration.GetValue<string>("AzureBlobStorage:ConnectionString");
        if (!string.IsNullOrEmpty(blobConnectionString))
        {
            services.AddSingleton(sp => new BlobServiceClient(blobConnectionString));
            services.AddSingleton<IBlobStorageClient>(sp =>
            {
                var blobClient = sp.GetRequiredService<BlobServiceClient>();
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BlobStorageClient>>();
                var containerName = configuration.GetValue<string>("AzureBlobStorage:ContainerName") ?? "device-bundles";
                return new BlobStorageClient(blobClient, logger, containerName);
            });
        }

        // Valkey (Redis) cache service
        var redisConnectionString = configuration.GetValue<string>("Valkey:ConnectionString");
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            services.AddSingleton<IConnectionMultiplexer>(sp =>
                ConnectionMultiplexer.Connect(redisConnectionString));
            services.AddSingleton<ICacheService, ValkeyCacheService>();
        }

        // Background services
        services.Configure<ApiKeyExpirationCheckOptions>(
            configuration.GetSection(ApiKeyExpirationCheckOptions.SectionName));

        // Register API key expiration check service if enabled
        var expirationCheckOptions = configuration
            .GetSection(ApiKeyExpirationCheckOptions.SectionName)
            .Get<ApiKeyExpirationCheckOptions>() ?? new ApiKeyExpirationCheckOptions();

        if (expirationCheckOptions.Enabled)
        {
            services.AddHostedService<ApiKeyExpirationCheckService>();
        }

        // Register dynamic group update service if enabled
        services.Configure<DynamicGroupUpdateOptions>(
            configuration.GetSection(DynamicGroupUpdateOptions.SectionName));

        var dynamicGroupUpdateOptions = configuration
            .GetSection(DynamicGroupUpdateOptions.SectionName)
            .Get<DynamicGroupUpdateOptions>() ?? new DynamicGroupUpdateOptions();

        if (dynamicGroupUpdateOptions.Enabled)
        {
            services.AddHostedService<DynamicGroupUpdateService>();
        }

        return services;
    }
}
