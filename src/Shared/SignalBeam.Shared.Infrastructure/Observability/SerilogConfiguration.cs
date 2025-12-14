using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace SignalBeam.Shared.Infrastructure.Observability;

/// <summary>
/// Extension methods for configuring Serilog.
/// </summary>
public static class SerilogConfiguration
{
    /// <summary>
    /// Creates a Serilog logger configuration for SignalBeam services.
    /// </summary>
    public static LoggerConfiguration CreateSignalBeamLogger(
        this LoggerConfiguration loggerConfiguration,
        IConfiguration configuration,
        string serviceName)
    {
        return loggerConfiguration
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("ServiceName", serviceName)
            .Enrich.WithProperty("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production")
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .WriteTo.Seq(
                serverUrl: configuration["Serilog:SeqServerUrl"] ?? "http://localhost:5341",
                restrictedToMinimumLevel: LogEventLevel.Information)
            .ReadFrom.Configuration(configuration);
    }

    /// <summary>
    /// Adds Loki sink to the Serilog configuration.
    /// </summary>
    public static LoggerConfiguration WithLokiSink(
        this LoggerConfiguration loggerConfiguration,
        string lokiUrl,
        string serviceName)
    {
        // Note: Loki sink requires Serilog.Sinks.Grafana.Loki package
        // This is a placeholder for future implementation
        // loggerConfiguration.WriteTo.GrafanaLoki(lokiUrl, labels: new[] { new LokiLabel { Key = "service", Value = serviceName } });

        return loggerConfiguration;
    }
}
