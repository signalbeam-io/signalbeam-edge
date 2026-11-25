using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using SignalBeam.ServiceDefaults.Observability;

namespace SignalBeam.ServiceDefaults;

public static class Extensions
{
    /// <summary>
    /// ActivitySource built into Npgsql. Covers every Postgres command, whether issued by
    /// EF Core or as raw ADO.NET commands on the EF connection. Spans use the stable OTel database
    /// conventions (db.system.name, db.namespace, db.query.text). Parameters appear as placeholders;
    /// only constants written in code (e.g. EF-inlined literals) appear inline.
    /// </summary>
    public const string NpgsqlActivitySourceName = "Npgsql";

    /// <summary>
    /// ActivitySource built into NATS.Net (Core and JetStream). Publish spans inject the W3C trace
    /// context into message headers and receive spans continue it, so asynchronous work joins the
    /// trace of the request that caused it. Spans use the OTel messaging conventions
    /// (messaging.system=nats, messaging.destination.name).
    /// </summary>
    public const string NatsActivitySourceName = "NATS.Net";

    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        // Process-wide NATS.Net instrumentation options: set before any NatsConnection is created.
        NatsTracing.Configure();

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource(NpgsqlActivitySourceName)
                    .AddSource(NatsActivitySourceName)
                    // Registered here, before the OTLP exporter (added in AddOpenTelemetryExporters),
                    // so the enriched names and tags are what gets exported.
                    .AddProcessor(new DatabaseSpanProcessor());
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder)
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Health check endpoints
        app.MapHealthChecks("/health");

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("ready")
        });

        return app;
    }
}
