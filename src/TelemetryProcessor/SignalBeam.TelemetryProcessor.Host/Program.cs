using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SignalBeam.ServiceDefaults;
using SignalBeam.Shared.Infrastructure.Authentication;
using SignalBeam.Shared.Infrastructure.Authentication.Authorization;
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

// A broker outage must not take down the whole service. The NATS background
// services (consumer + SSE bridge) retry with backoff and recover on their own
// (#387); Ignore remains as a guard so an unexpected fatal exception in any
// other background service degrades that feature instead of crash-looping the
// host (which serves HTTP + health).
builder.Services.Configure<HostOptions>(options =>
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

// Add TelemetryProcessor Infrastructure (DbContext, NATS, Repositories, Message Handlers)
builder.Services.AddTelemetryProcessorInfrastructure(builder.Configuration);

// Add TelemetryProcessor Health Checks (PostgreSQL, NATS)
builder.Services.AddTelemetryProcessorHealthChecks(builder.Configuration);

// The alert API is an operator API: Zitadel JWT, or outside Production the tenant API key (#431),
// the same as BundleOrchestrator. Other endpoints keep their existing (unauthenticated) behaviour.
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwtConfig = builder.Configuration.GetSection("Authentication:Jwt");
    options.Authority = jwtConfig["Authority"];
    options.Audience = GetAudienceFromZitadelConfig(builder.Configuration) ?? jwtConfig["Audience"];
    options.RequireHttpsMetadata = jwtConfig.GetValue<bool>("RequireHttpsMetadata", true);
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.FromMinutes(5)
    };
});
builder.Services.AddSingleton<IApiKeyValidator, ApiKeyValidator>();
builder.Services.AddOperatorAuthorization(!builder.Environment.IsProduction());

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

// Apply database migrations automatically (skip in Testing environment)
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<SignalBeam.TelemetryProcessor.Infrastructure.Persistence.TelemetryDbContext>();

        logger.LogInformation("Applying database migrations...");
        await context.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while applying database migrations.");
        throw;
    }
}

// Configure the HTTP request pipeline
app.UseSerilogRequestLogging();

// API key / JWT authentication for the alert API only; the metrics stream and health endpoints
// are not covered (yet).
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api/alerts"),
    alerts => alerts.UseApiKeyAuthentication());
app.UseAuthentication();
app.UseAuthorization();

// Map Aspire default endpoints (/health, /health/live, /health/ready, /metrics)
app.MapDefaultEndpoints();

// Map Alert Management API endpoints
app.MapAlertEndpoints();

// Map real-time metrics streaming endpoints (SSE)
app.MapMetricsStreamEndpoints();

// Map a root endpoint for basic service info
app.MapGet("/", () => new
{
    service = "SignalBeam TelemetryProcessor",
    version = "1.0.0",
    status = "running"
});

app.Run();

static string? GetAudienceFromZitadelConfig(IConfiguration configuration)
{
    var configPath = configuration["ZITADEL_CONFIG_PATH"];
    if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
    {
        return null;
    }

    try
    {
        var config = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(configPath));
        return config.TryGetProperty("backend", out var backend) && backend.TryGetProperty("audience", out var audience)
            ? audience.GetString()
            : null;
    }
    catch (Exception ex) when (ex is IOException or JsonException)
    {
        Console.WriteLine($"Warning: Failed to read Zitadel config from {configPath}: {ex.Message}");
        return null;
    }
}

// Make Program accessible to WebApplicationFactory in tests
public partial class Program { }
