using SignalBeam.IdentityManager.Application.Commands;
using SignalBeam.IdentityManager.Application.Queries;
using SignalBeam.IdentityManager.Infrastructure;
using SignalBeam.IdentityManager.Host.Endpoints;
using Serilog;
using SignalBeam.ServiceDefaults;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using SignalBeam.IdentityManager.Infrastructure.Persistence;

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
        ValidateAudience = false, // Temporarily disabled for debugging
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.FromMinutes(5)
    };

    // Add event handlers for debugging
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"[JWT] Authentication failed: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine($"[JWT] Token validated successfully for user: {context.Principal?.Identity?.Name}");
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

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

// Register CQRS handlers
builder.Services.AddScoped<RegisterUserHandler>();
builder.Services.AddScoped<GetCurrentUserHandler>();
builder.Services.AddScoped<UpgradeSubscriptionHandler>();
builder.Services.AddScoped<GetTenantsWithRetentionHandler>();

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
            new OpenApiServer { Url = "https://api.signalbeam.local", Description = "Local Kubernetes" },
            new OpenApiServer { Url = "https://api-dev.signalbeam.io", Description = "Development Environment" },
            new OpenApiServer { Url = "https://api-staging.signalbeam.io", Description = "Staging Environment" },
            new OpenApiServer { Url = "https://api.signalbeam.io", Description = "Production Environment" }
        };
        return Task.CompletedTask;
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
            var context = services.GetRequiredService<IdentityDbContext>();
            var logger = services.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("Applying database migrations...");
            context.Database.Migrate();
            logger.LogInformation("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred during database migration.");
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
            .WithTitle("SignalBeam Identity Manager API")
            .WithTheme(ScalarTheme.Purple)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseCors("WebDev");
}

// Add standard authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Map Aspire default endpoints (/health, /health/live, /health/ready)
app.MapDefaultEndpoints();

// Map API endpoints
app.MapAuthEndpoints();
app.MapSubscriptionEndpoints();
app.MapTenantEndpoints();

app.Run();

// Make Program accessible to WebApplicationFactory in tests
public partial class Program { }
