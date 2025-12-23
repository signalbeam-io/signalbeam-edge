using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SignalBeam.EdgeAgent.Host.Configuration;
using SignalBeam.EdgeAgent.Host.Services;
using SignalBeam.EdgeAgent.Infrastructure;
using Wolverine;

namespace SignalBeam.EdgeAgent.Host;

public static class HostBuilder
{
    public static IHost BuildHost()
    {
        var configuration = BuildConfiguration();

        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .UseSerilog()
            .UseSystemd() // Enable systemd integration for Type=notify support
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddConfiguration(configuration);
            })
            .ConfigureServices((context, services) =>
            {
                // Configuration
                services.Configure<AgentOptions>(context.Configuration.GetSection(AgentOptions.SectionName));

                // State management
                services.AddSingleton<DeviceStateManager>();

                // Infrastructure
                services.AddInfrastructure();

                // Background services
                services.AddHostedService<HeartbeatService>();
                services.AddHostedService<ReconciliationService>();
            })
            .UseWolverine()
            .Build();

        return host;
    }

    public static IServiceProvider BuildServiceProvider()
    {
        var configuration = BuildConfiguration();

        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        var services = new ServiceCollection();

        // Configuration
        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<AgentOptions>(configuration.GetSection(AgentOptions.SectionName));

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddSerilog();
        });

        // State management
        services.AddSingleton<DeviceStateManager>();

        // Infrastructure
        services.AddInfrastructure();

        return services.BuildServiceProvider();
    }

    private static IConfiguration BuildConfiguration()
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        return configuration;
    }
}
