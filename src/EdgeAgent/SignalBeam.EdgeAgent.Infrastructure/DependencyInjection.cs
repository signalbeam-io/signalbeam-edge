using Microsoft.Extensions.DependencyInjection;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.EdgeAgent.Infrastructure.Container;

namespace SignalBeam.EdgeAgent.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Register Docker container manager
        services.AddSingleton<IContainerManager, DockerContainerManager>();

        return services;
    }
}
