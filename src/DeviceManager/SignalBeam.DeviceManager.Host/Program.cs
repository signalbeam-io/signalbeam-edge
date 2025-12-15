using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Application.Validators;
using SignalBeam.DeviceManager.Host.Endpoints;
using SignalBeam.DeviceManager.Infrastructure;
using FluentValidation;
using Serilog;
using SignalBeam.ServiceDefaults;
using Wolverine;
using Wolverine.Http;

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

// Add Wolverine for CQRS
builder.Host.UseWolverine(opts =>
{
    // Discover handlers from Application assembly
    opts.Discovery.IncludeAssembly(typeof(RegisterDeviceHandler).Assembly);

    // Use FluentValidation middleware
    opts.Policies.AutoApplyTransactions();
});

// Add OpenAPI and Scalar
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // TODO: Configure Scalar API documentation
    // app.MapScalarApiReference();
}

app.UseSerilogRequestLogging();

// Map Aspire default endpoints (/health, /health/live, /health/ready)
app.MapDefaultEndpoints();

// Map Wolverine HTTP endpoints
app.MapWolverineEndpoints();

// Map API endpoints
app.MapDeviceEndpoints();

app.Run();
