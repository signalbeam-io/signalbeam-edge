using SignalBeam.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations
builder.AddServiceDefaults();

// Add YARP reverse proxy with service discovery
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

// Add CORS to allow frontend requests. Local dev origins are always allowed;
// deployed origins (e.g. the Azure Static Web Apps dashboard) are supplied via
// configuration so they can be set per-environment without a code change:
//   Cors__AllowedOrigins__0 = https://<app>.azurestaticapps.net
var defaultOrigins = new[]
{
    "http://localhost:5173",  // Vite dev server (Aspire proxy)
    "http://localhost:5174",  // Vite dev server (fallback port)
    "http://localhost:3000",  // Alternative frontend port
    "http://localhost:3001",  // Alternative frontend port
    "http://localhost:4173",  // Vite preview
};

var configuredOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

// Fail fast on misconfiguration: a wildcard or malformed origin must never reach
// the CORS policy (a public SPA origin combined with AllowCredentials makes "*"
// both invalid and dangerous). Validate before the policy is built.
var invalidOrigins = configuredOrigins
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Where(origin => origin == "*" || !Uri.TryCreate(origin, UriKind.Absolute, out _))
    .ToArray();

if (invalidOrigins.Length > 0)
{
    throw new InvalidOperationException(
        $"Invalid CORS origins in configuration (Cors:AllowedOrigins): {string.Join(", ", invalidOrigins)}. " +
        "Origins must be absolute URIs (e.g. https://app.azurestaticapps.net) and must not be '*'.");
}

var allowedOrigins = defaultOrigins
    .Concat(configuredOrigins)
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .WithExposedHeaders("*");  // Expose all response headers
    });
});

var app = builder.Build();

// Map default endpoints (health checks, metrics)
app.MapDefaultEndpoints();

// Enable CORS
app.UseCors();

// Map YARP reverse proxy
app.MapReverseProxy();

app.Run();
