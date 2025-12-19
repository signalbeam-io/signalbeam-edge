using Serilog;
using SignalBeam.ServiceDefaults;
using SignalBeam.TelemetryProcessor.Application.BackgroundServices;
using SignalBeam.TelemetryProcessor.Application.Commands;
using SignalBeam.TelemetryProcessor.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog
builder.Host.UseSerilog((context, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);
});

// Add Aspire ServiceDefaults (OpenTelemetry, Service Discovery, Health Checks)
builder.AddServiceDefaults();

// Add TelemetryProcessor Infrastructure (DbContext, NATS, Repositories, Message Handlers)
builder.Services.AddTelemetryProcessorInfrastructure(builder.Configuration);

// Add TelemetryProcessor Health Checks (PostgreSQL, NATS)
builder.Services.AddTelemetryProcessorHealthChecks(builder.Configuration);

// Configure DeviceStatusMonitor options
builder.Services.Configure<DeviceStatusMonitorOptions>(
    builder.Configuration.GetSection("DeviceStatusMonitor"));

// Configure MetricsAggregationService options
builder.Services.Configure<MetricsAggregationOptions>(
    builder.Configuration.GetSection("MetricsAggregation"));

// Register command handlers from Application layer
builder.Services.AddScoped<ProcessHeartbeatHandler>();
builder.Services.AddScoped<ProcessMetricsHandler>();

// Register background services from Application layer
builder.Services.AddHostedService<DeviceStatusMonitor>();
builder.Services.AddHostedService<MetricsAggregationService>();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSerilogRequestLogging();

// Map Aspire default endpoints (/health, /health/live, /health/ready, /metrics)
app.MapDefaultEndpoints();

// Map a root endpoint for basic service info
app.MapGet("/", () => new
{
    service = "SignalBeam TelemetryProcessor",
    version = "1.0.0",
    status = "running"
});

app.Run();

// Make Program accessible to WebApplicationFactory in tests
public partial class Program { }
