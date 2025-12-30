using Serilog;
using SignalBeam.ServiceDefaults;
using SignalBeam.TelemetryProcessor.Application.BackgroundServices;
using SignalBeam.TelemetryProcessor.Application.Commands;
using SignalBeam.TelemetryProcessor.Application.Queries;
using SignalBeam.TelemetryProcessor.Host.Endpoints;
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

// Configure HealthMonitor options
builder.Services.Configure<HealthMonitorOptions>(
    builder.Configuration.GetSection(HealthMonitorOptions.SectionName));

// Configure AlertManager options
builder.Services.Configure<AlertManagerOptions>(
    builder.Configuration.GetSection(AlertManagerOptions.SectionName));

// Configure NotificationDispatcher options
builder.Services.Configure<NotificationDispatcherOptions>(
    builder.Configuration.GetSection(NotificationDispatcherOptions.SectionName));

// Configure NotificationRetry options
builder.Services.Configure<NotificationRetryOptions>(
    builder.Configuration.GetSection(NotificationRetryOptions.SectionName));

// Register command handlers from Application layer
builder.Services.AddScoped<ProcessHeartbeatHandler>();
builder.Services.AddScoped<ProcessMetricsHandler>();
builder.Services.AddScoped<AcknowledgeAlertHandler>();
builder.Services.AddScoped<ResolveAlertHandler>();

// Register query handlers from Application layer
builder.Services.AddScoped<GetAlertsHandler>();
builder.Services.AddScoped<GetAlertByIdHandler>();
builder.Services.AddScoped<GetAlertStatisticsHandler>();

// Register background services from Application layer
builder.Services.AddHostedService<DeviceStatusMonitor>();
builder.Services.AddHostedService<MetricsAggregationService>();
builder.Services.AddHostedService<HealthMonitorService>();
builder.Services.AddHostedService<AlertManagerService>();
builder.Services.AddHostedService<NotificationDispatcherService>();
builder.Services.AddHostedService<NotificationRetryService>();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSerilogRequestLogging();

// Map Aspire default endpoints (/health, /health/live, /health/ready, /metrics)
app.MapDefaultEndpoints();

// Map Alert Management API endpoints
app.MapAlertEndpoints();

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
