using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using SignalBeam.ServiceDefaults;

namespace SignalBeam.TrafficGenerator;

internal static class TrafficTelemetry
{
    public const string SourceName = "SignalBeam.TrafficGenerator";

    public static readonly ActivitySource Source = new(SourceName);

    // A minimal host only to run the ServiceDefaults OpenTelemetry pipeline (HttpClient
    // instrumentation + OTLP export when Aspire sets OTEL_EXPORTER_OTLP_ENDPOINT).
    public static IHost BuildHost()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = [] });
        builder.Services.Configure<ConsoleLifetimeOptions>(o => o.SuppressStatusMessages = true);
        builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(5));
        builder.ConfigureOpenTelemetry();
        builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddSource(SourceName));
        return builder.Build();
    }
}
