using SignalBeam.BundleOrchestrator.Application.Commands;
using SignalBeam.BundleOrchestrator.Application.Queries;
using SignalBeam.BundleOrchestrator.Application.Services;
using SignalBeam.BundleOrchestrator.Application.Validators;
using SignalBeam.BundleOrchestrator.Host.BackgroundServices;
using SignalBeam.BundleOrchestrator.Host.Endpoints;
using SignalBeam.BundleOrchestrator.Infrastructure;
using FluentValidation;
using Serilog;
using SignalBeam.ServiceDefaults;
using Scalar.AspNetCore;
using SignalBeam.Shared.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog
builder.Host.UseSerilog((context, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);
});

// Add Aspire ServiceDefaults (OpenTelemetry, Service Discovery, Health Checks)
builder.AddServiceDefaults();

// Add Azure Blob Storage (Azurite locally, Azure Blob Storage in production)
builder.AddAzureBlobClient("blobs");

// Add Infrastructure (DbContext, Repositories)
builder.Services.AddInfrastructure(builder.Configuration);

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<CreateBundleValidator>();

// Add Authentication and Authorization
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwtConfig = builder.Configuration.GetSection("Authentication:Jwt");
    options.Authority = jwtConfig["Authority"];
    options.Audience = jwtConfig["Audience"];
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

builder.Services.AddAuthorization();

// Add Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        // Get tenant ID from claims or API key
        var tenantId = context.User.FindFirst(AuthenticationConstants.TenantIdClaimType)?.Value ?? "anonymous";

        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: tenantId,
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        double? retryAfterSeconds = null;
        if (context.Lease.TryGetMetadata(System.Threading.RateLimiting.MetadataName.RetryAfter, out var retryAfter))
        {
            retryAfterSeconds = retryAfter.TotalSeconds;
        }

        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "RATE_LIMIT_EXCEEDED",
            message = "Too many requests. Please try again later.",
            retryAfter = retryAfterSeconds
        }, cancellationToken);
    };
});

// Add CORS for local web UI development
builder.Services.AddCors(options =>
{
    options.AddPolicy("WebDev", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Register CQRS handlers manually (we're using manual endpoints, not Wolverine HTTP)
builder.Services.AddScoped<CreateBundleHandler>();
builder.Services.AddScoped<CreateBundleVersionHandler>();
builder.Services.AddScoped<UploadBundleHandler>();
builder.Services.AddScoped<UploadBundleVersionHandler>();
builder.Services.AddScoped<AssignBundleToDeviceHandler>();
builder.Services.AddScoped<AssignBundleToGroupHandler>();
builder.Services.AddScoped<UpdateRolloutStatusHandler>();

builder.Services.AddScoped<GetBundlesHandler>();
builder.Services.AddScoped<GetBundleByIdHandler>();
builder.Services.AddScoped<GetBundleVersionHandler>();
builder.Services.AddScoped<GetBundleDefinitionHandler>();
builder.Services.AddScoped<GetLatestBundleDefinitionHandler>();
builder.Services.AddScoped<GetBundleAssignedDevicesHandler>();
builder.Services.AddScoped<GetDeviceDesiredStateHandler>();
builder.Services.AddScoped<GetRolloutStatusHandler>();

// Unified Rollout handlers
builder.Services.AddScoped<CreateRolloutHandler>();
builder.Services.AddScoped<GetRolloutsHandler>();
builder.Services.AddScoped<GetRolloutByIdHandler>();
builder.Services.AddScoped<GetRolloutDevicesHandler>();
builder.Services.AddScoped<CancelRolloutHandler>();

// Phased Rollout handlers
builder.Services.AddScoped<CreatePhasedRolloutHandler>();
builder.Services.AddScoped<StartRolloutHandler>();
builder.Services.AddScoped<PauseRolloutHandler>();
builder.Services.AddScoped<ResumeRolloutHandler>();
builder.Services.AddScoped<RollbackRolloutHandler>();
builder.Services.AddScoped<AdvancePhaseHandler>();
builder.Services.AddScoped<GetPhasedRolloutDetailsHandler>();
builder.Services.AddScoped<ListPhasedRolloutsHandler>();
builder.Services.AddScoped<GetActiveRolloutsHandler>();
builder.Services.AddScoped<GetBundleRolloutHistoryHandler>();

// Rollout orchestration services
builder.Services.AddScoped<RolloutOrchestrationService>();

// Configure rollout orchestrator options
builder.Services.Configure<RolloutOrchestratorOptions>(
    builder.Configuration.GetSection(RolloutOrchestratorOptions.SectionName));

// Add background services
builder.Services.AddHostedService<RolloutOrchestratorWorker>();

// Add OpenAPI and Scalar
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        // Add multiple server URLs to allow switching in Scalar
        document.Servers = new List<OpenApiServer>
        {
            new OpenApiServer { Url = "/", Description = "Current Server (Auto-detected)" },
            new OpenApiServer { Url = "http://localhost:5002", Description = "Local Development (Port 5002)" },
            new OpenApiServer { Url = "http://localhost:5003", Description = "Alternative Local Port" },
            new OpenApiServer { Url = "https://api.signalbeam.local", Description = "Local Kubernetes" },
            new OpenApiServer { Url = "https://api-dev.signalbeam.io", Description = "Development Environment" },
            new OpenApiServer { Url = "https://api-staging.signalbeam.io", Description = "Staging Environment" },
            new OpenApiServer { Url = "https://api.signalbeam.io", Description = "Production Environment" }
        };
        return Task.CompletedTask;
    });
});

var app = builder.Build();

// Apply database migrations automatically
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<SignalBeam.BundleOrchestrator.Infrastructure.Persistence.BundleDbContext>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Applying database migrations...");
        context.Database.Migrate();
        logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
        throw;
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("SignalBeam Bundle Orchestrator API")
            .WithTheme(ScalarTheme.Purple)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseCors("WebDev");
}

// Add API key authentication middleware (for edge devices)
app.UseApiKeyAuthentication();

// Add standard authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Add rate limiting
app.UseRateLimiter();

// Map Aspire default endpoints (/health, /health/live, /health/ready)
app.MapDefaultEndpoints();

// Map API endpoints
app.MapBundleEndpoints();
app.MapBundleVersionEndpoints();
app.MapBundleAssignmentEndpoints();
app.MapRolloutStatusEndpoints();
app.MapRolloutEndpoints(); // Unified Rollout API
app.MapPhasedRolloutEndpoints(); // Phased Rollout Management API

app.Run();

// Make Program accessible to WebApplicationFactory in tests
public partial class Program { }
