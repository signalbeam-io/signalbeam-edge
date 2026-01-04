using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Application.Queries;
using SignalBeam.DeviceManager.Application.Validators;
using SignalBeam.DeviceManager.Host.Endpoints;
using SignalBeam.DeviceManager.Host.Middleware;
using SignalBeam.DeviceManager.Infrastructure;
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

// Add Infrastructure (DbContext, Repositories)
builder.Services.AddInfrastructure(builder.Configuration);

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<RegisterDeviceValidator>();

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
builder.Services.AddScoped<RegisterDeviceHandler>();
builder.Services.AddScoped<UpdateDeviceHandler>();
builder.Services.AddScoped<AddDeviceTagHandler>();
builder.Services.AddScoped<RecordHeartbeatHandler>();
builder.Services.AddScoped<AssignDeviceToGroupHandler>();
builder.Services.AddScoped<ReportDeviceStateHandler>();
builder.Services.AddScoped<UpdateDeviceMetricsHandler>();
builder.Services.AddScoped<CreateDeviceGroupHandler>();
builder.Services.AddScoped<UpdateDeviceGroupHandler>();
builder.Services.AddScoped<AddDeviceToGroupHandler>();
builder.Services.AddScoped<RemoveDeviceFromGroupHandler>();
builder.Services.AddScoped<RemoveDeviceTagHandler>();
builder.Services.AddScoped<BulkAddDeviceTagsHandler>();
builder.Services.AddScoped<BulkRemoveDeviceTagsHandler>();

builder.Services.AddScoped<GetDevicesHandler>();
builder.Services.AddScoped<GetDeviceByIdHandler>();
builder.Services.AddScoped<GetDeviceHealthHandler>();
builder.Services.AddScoped<GetDevicesByGroupHandler>();
builder.Services.AddScoped<GetDeviceActivityLogHandler>();
builder.Services.AddScoped<GetDeviceMetricsHandler>();
builder.Services.AddScoped<GetDeviceGroupsHandler>();
builder.Services.AddScoped<GetDevicesByTagQueryHandler>();
builder.Services.AddScoped<GetAllTagsHandler>();
builder.Services.AddScoped<GetGroupMembershipsHandler>();

// Register certificate-related services
// Note: ICertificateGenerator and ICertificateAuthorityService are registered in DependencyInjection.cs
builder.Services.AddScoped<SignalBeam.Shared.Infrastructure.Authentication.IDeviceCertificateValidator,
    SignalBeam.DeviceManager.Infrastructure.Authentication.DeviceCertificateValidator>();

// Register certificate command and query handlers
builder.Services.AddScoped<IssueCertificateHandler>();
builder.Services.AddScoped<RenewCertificateHandler>();
builder.Services.AddScoped<RevokeCertificateHandler>();
builder.Services.AddScoped<GetDeviceCertificatesHandler>();

// Add OpenAPI and Scalar
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        // Add multiple server URLs to allow switching in Scalar
        document.Servers = new List<OpenApiServer>
        {
            new OpenApiServer { Url = "/", Description = "Current Server (Auto-detected)" },
            new OpenApiServer { Url = "http://localhost:5000", Description = "Local Development (Port 5000)" },
            new OpenApiServer { Url = "http://localhost:5001", Description = "Alternative Local Port" },
            new OpenApiServer { Url = "https://api.signalbeam.local", Description = "Local Kubernetes" },
            new OpenApiServer { Url = "https://api-dev.signalbeam.io", Description = "Development Environment" },
            new OpenApiServer { Url = "https://api-staging.signalbeam.io", Description = "Staging Environment" },
            new OpenApiServer { Url = "https://api.signalbeam.io", Description = "Production Environment" }
        };
        return Task.CompletedTask;
    });
});

// Configure Kestrel for client certificate authentication (mTLS)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureHttpsDefaults(httpsOptions =>
    {
        // Allow but don't require client certificates
        // Validation happens in DeviceAuthenticationMiddleware
        httpsOptions.ClientCertificateMode = Microsoft.AspNetCore.Server.Kestrel.Https.ClientCertificateMode.AllowCertificate;
        httpsOptions.AllowAnyClientCertificate();
        httpsOptions.CheckCertificateRevocation = false; // We handle revocation in DB
    });
});

var app = builder.Build();

// Apply database migrations automatically (skip in Testing environment)
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<SignalBeam.DeviceManager.Infrastructure.Persistence.DeviceDbContext>();
            var logger = services.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("Applying database migrations...");
            context.Database.Migrate();
            logger.LogInformation("Database migrations applied successfully.");

            // Initialize Certificate Authority (only in production environments)
            if (app.Environment.IsProduction() || app.Environment.IsDevelopment())
            {
                logger.LogInformation("Initializing Certificate Authority...");
                var caService = services.GetRequiredService<SignalBeam.DeviceManager.Application.Services.ICertificateAuthorityService>();
                await caService.InitializeAsync();
                logger.LogInformation("Certificate Authority initialized successfully.");
            }
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred during startup initialization.");
            throw;
        }
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("SignalBeam Device Manager API")
            .WithTheme(ScalarTheme.Purple)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

// Add correlation ID middleware (should be early in pipeline)
app.UseCorrelationId();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseCors("WebDev");
}

// Add unified device authentication middleware (supports both mTLS and API keys)
app.UseDeviceAuthentication();

// Add standard authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Add rate limiting
app.UseRateLimiter();

// Map Aspire default endpoints (/health, /health/live, /health/ready)
app.MapDefaultEndpoints();

// Map API endpoints
app.MapDeviceEndpoints();
app.MapGroupEndpoints();
app.MapTagEndpoints();
app.MapCertificateEndpoints();

app.Run();

// Make Program accessible to WebApplicationFactory in tests
public partial class Program { }
