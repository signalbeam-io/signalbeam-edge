using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SignalBeam.EdgeAgent.Application.Commands;
using SignalBeam.EdgeAgent.Host.Configuration;
using SignalBeam.EdgeAgent.Host.Services;
using SignalBeam.EdgeAgent.Infrastructure;
using SignalBeam.EdgeAgent.Infrastructure.BackgroundServices;
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
                services.AddSingleton(_ => CreateDeviceStateManager(context.Configuration));

                // Infrastructure
                services.AddInfrastructure(context.Configuration);

                // Application handlers resolved directly by background services
                services.AddScoped<CheckRegistrationStatusCommandHandler>();
                services.AddScoped<RequestCertificateCommandHandler>();

                // Background services
                services.AddHostedService<RegistrationPollingService>();
                services.AddHostedService<KeyLifecycleService>();
                services.AddHostedService<HeartbeatService>();
                services.AddHostedService<ReconciliationService>();
                services.AddHostedService<NatsAssignmentListenerService>();

                // Certificate auto-renewal
                services.Configure<CertificateRenewalOptions>(
                    context.Configuration.GetSection(CertificateRenewalOptions.SectionName));

                var certRenewalOptions = context.Configuration
                    .GetSection(CertificateRenewalOptions.SectionName)
                    .Get<CertificateRenewalOptions>() ?? new CertificateRenewalOptions();

                if (certRenewalOptions.Enabled)
                {
                    services.AddHostedService<CertificateRenewalBackgroundService>();
                }
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
        services.AddSingleton(_ => CreateDeviceStateManager(configuration));

        // Infrastructure
        services.AddInfrastructure(configuration);

        // Application handlers used by the CLI commands
        services.AddScoped<RegisterDeviceCommandHandler>();
        services.AddScoped<CheckRegistrationStatusCommandHandler>();
        services.AddScoped<RequestCertificateCommandHandler>();

        return services.BuildServiceProvider();
    }

    internal static IConfiguration BuildConfiguration() => BuildConfiguration(AgentPaths.ConfigFilePath);

    /// <summary>
    /// Precedence (last wins): appsettings.json → appsettings.{env}.json → machine config file
    /// (written by <c>register</c>) → environment variables.
    /// </summary>
    internal static IConfiguration BuildConfiguration(string machineConfigFilePath)
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";

        // Resolve appsettings next to the binary, not the working directory: under systemd the
        // agent runs from /var/lib/signalbeam while the .deb installs appsettings in /usr/local/bin.
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddJsonFile(machineConfigFilePath, optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        return configuration;
    }

    private static DeviceStateManager CreateDeviceStateManager(IConfiguration configuration) =>
        new(configuration["Agent:StateDirectory"] ?? AgentPaths.DefaultStateDirectory);
}
