using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SignalBeam.IdentityManager.Application.Repositories;
using SignalBeam.IdentityManager.Application.Services;
using SignalBeam.IdentityManager.Infrastructure.Persistence;
using SignalBeam.IdentityManager.Infrastructure.Persistence.Repositories;
using SignalBeam.IdentityManager.Infrastructure.Services;

namespace SignalBeam.IdentityManager.Infrastructure;

/// <summary>
/// Dependency injection configuration for IdentityManager Infrastructure layer.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register DbContext
        services.AddDbContext<IdentityDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("signalbeam")
                ?? throw new InvalidOperationException("Database connection string 'signalbeam' not found.");

            options.UseNpgsql(connectionString);
        });

        // Register repositories
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();

        // Register services
        services.AddScoped<IQuotaEnforcementService, QuotaEnforcementService>();

        return services;
    }
}
