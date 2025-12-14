using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace SignalBeam.Shared.Infrastructure.Observability;

/// <summary>
/// Extension methods for configuring OpenTelemetry.
/// </summary>
public static class OpenTelemetryConfiguration
{
    /// <summary>
    /// Adds OpenTelemetry with tracing and metrics to the service collection.
    /// </summary>
    public static IServiceCollection AddSignalBeamOpenTelemetry(
        this IServiceCollection services,
        string serviceName,
        string serviceVersion,
        Action<TracerProviderBuilder>? configureTracing = null,
        Action<MeterProviderBuilder>? configureMetrics = null)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource.AddService(
                    serviceName: serviceName,
                    serviceVersion: serviceVersion,
                    serviceInstanceId: Environment.MachineName);
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(ActivityNames.SignalBeam)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = httpContext =>
                        {
                            // Don't trace health check endpoints
                            var path = httpContext.Request.Path.Value ?? string.Empty;
                            return !path.Contains("/health") && !path.Contains("/metrics");
                        };
                    })
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation(options =>
                    {
                        options.SetDbStatementForText = true;
                        options.EnrichWithIDbCommand = (activity, command) =>
                        {
                            activity.SetTag("db.query", command.CommandText);
                        };
                    });

                configureTracing?.Invoke(tracing);

                // Add OTLP exporter
                tracing.AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(MetricNames.SignalBeam)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                configureMetrics?.Invoke(metrics);

                // Add OTLP exporter
                metrics.AddOtlpExporter();

                // Add Prometheus exporter for scraping
                metrics.AddPrometheusExporter();
            });

        return services;
    }

    /// <summary>
    /// Adds OpenTelemetry with default configuration for SignalBeam services.
    /// </summary>
    public static IServiceCollection AddSignalBeamOpenTelemetry(
        this IServiceCollection services,
        string serviceName,
        string serviceVersion = "1.0.0")
    {
        return services.AddSignalBeamOpenTelemetry(serviceName, serviceVersion, null, null);
    }
}
