using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.DeviceManager.Infrastructure.Persistence;
using SignalBeam.DeviceManager.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            var connectionString = configuration.GetConnectionString("DeviceDb");
            options.UseNpgsql(connectionString);
        });

        // Register repositories
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IDeviceQueryRepository, DeviceRepository>();
        services.AddScoped<IDeviceMetricsRepository, DeviceMetricsRepository>();
        services.AddScoped<IDeviceActivityLogRepository, DeviceActivityLogRepository>();
        services.AddScoped<IDeviceActivityLogQueryRepository, DeviceActivityLogRepository>();

        return services;
    }
}
