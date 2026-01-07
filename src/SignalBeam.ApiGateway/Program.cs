using SignalBeam.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations
builder.AddServiceDefaults();

// Add YARP reverse proxy with service discovery
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

// Add CORS to allow frontend requests
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",  // Vite dev server (default)
                "http://localhost:3000",  // Alternative frontend port
                "http://localhost:3001",  // Alternative frontend port
                "http://localhost:4173"   // Vite preview
            )
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
