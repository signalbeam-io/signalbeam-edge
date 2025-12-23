# Running SignalBeam with .NET Aspire

This guide shows how to run the SignalBeam DeviceManager service using .NET Aspire for local development.

## Prerequisites

- .NET 9.0 SDK
- Docker Desktop (running)
- .NET Aspire workload installed:
  ```bash
  dotnet workload install aspire
  ```

## Running the Application

### Option 1: Using Aspire AppHost (Recommended)

Run the entire stack (PostgreSQL, Valkey, NATS, DeviceManager) with a single command:

```bash
cd src/SignalBeam.AppHost
dotnet run
```

The Aspire Dashboard will automatically open in your browser at `http://localhost:15888`

### Option 2: Running DeviceManager Standalone

If you prefer to run just the DeviceManager service:

```bash
cd src/DeviceManager/SignalBeam.DeviceManager.Host
dotnet run
```

**Note:** You'll need PostgreSQL, Valkey, and NATS running separately.

## Aspire Dashboard Features

When running with Aspire, you get:

### 📊 **Unified Dashboard** (`http://localhost:15888`)
- View all services and containers in one place
- Real-time logs from all services
- Distributed traces across services
- Resource metrics (CPU, memory)

### 🔧 **Infrastructure Services**

The AppHost automatically starts:

1. **PostgreSQL** (with pgAdmin)
   - Database: `signalbeam`
   - pgAdmin available at configured port
   - Persistent storage

2. **Valkey** (Redis-compatible cache)
   - Drop-in Redis replacement
   - Persistent storage

3. **NATS** (with JetStream)
   - Messaging and event streaming
   - Management UI at port 8222
   - JetStream for persistent messaging

### 🚀 **DeviceManager Service**

The DeviceManager API runs with:
- **OpenAPI/Swagger**: `http://localhost:<port>/openapi/v1.json`
- **Scalar API Docs**: `http://localhost:<port>/scalar/v1`
- **Health Checks**: `http://localhost:<port>/health`
- **Metrics**: `http://localhost:<port>/metrics`

## API Endpoints

Once the DeviceManager is running, you can access:

### Device Endpoints
- `POST /api/devices` - Register a new device
- `GET /api/devices` - List all devices
- `GET /api/devices/{id}` - Get device details
- `PUT /api/devices/{id}` - Update device
- `POST /api/devices/{id}/heartbeat` - Record heartbeat
- `GET /api/devices/{id}/metrics` - Get metrics history
- `POST /api/devices/{id}/metrics` - Update metrics
- `GET /api/devices/{id}/activity-log` - Get activity log
- `POST /api/devices/{id}/tags` - Add tag to device

### Group Endpoints
- `GET /api/groups` - List device groups
- `POST /api/groups` - Create device group
- `POST /api/groups/{id}/devices` - Add device to group

## Authentication

The DeviceManager API supports two authentication methods:

### 1. API Key Authentication (for edge devices)

Add the `X-Api-Key` header to your requests:

```bash
curl -H "X-Api-Key: dev-api-key-1" http://localhost:<port>/api/devices
```

**Development API Keys** (from appsettings.json):
- `dev-api-key-1` - Full access (tenant: 00000000-0000-0000-0000-000000000001)
- `dev-api-key-2` - Read-only (tenant: 00000000-0000-0000-0000-000000000002)

### 2. JWT Bearer Authentication (for web/dashboard)

Configure Azure AD/Entra ID in `appsettings.json`:

```json
{
  "Authentication": {
    "Jwt": {
      "Authority": "https://login.microsoftonline.com/your-tenant-id/v2.0",
      "Audience": "api://signalbeam-device-manager"
    }
  }
}
```

## Testing the API

### Using Scalar UI (Recommended)

1. Start the AppHost: `dotnet run`
2. Open Scalar UI: `http://localhost:<device-manager-port>/scalar/v1`
3. Click "Try it out" on any endpoint
4. Add `X-Api-Key: dev-api-key-1` header
5. Execute the request

### Using curl

Register a device:
```bash
curl -X POST http://localhost:<port>/api/devices \
  -H "X-Api-Key: dev-api-key-1" \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "00000000-0000-0000-0000-000000000001",
    "deviceId": "00000000-0000-0000-0000-000000000001",
    "name": "Test Device"
  }'
```

List devices:
```bash
curl -X GET "http://localhost:<port>/api/devices?tenantId=00000000-0000-0000-0000-000000000001" \
  -H "X-Api-Key: dev-api-key-1"
```

## Aspire Configuration

The AppHost configuration (`src/SignalBeam.AppHost/Program.cs`):

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

var signalbeamDb = postgres.AddDatabase("signalbeam");

var valkey = builder.AddRedis("valkey")
    .WithLifetime(ContainerLifetime.Persistent);

var nats = builder.AddContainer("nats", "nats", "latest")
    .WithArgs("--jetstream")
    .WithHttpEndpoint(8222, targetPort: 8222, name: "management")
    .WithEndpoint(4222, targetPort: 4222, name: "nats")
    .WithLifetime(ContainerLifetime.Persistent);

// DeviceManager Service
var deviceManager = builder.AddProject<Projects.SignalBeam_DeviceManager_Host>("device-manager")
    .WithReference(signalbeamDb)
    .WithReference(valkey)
    .WithEnvironment("NATS__Url", nats.GetEndpoint("nats"));

builder.Build().Run();
```

## Rate Limiting

The API includes per-tenant rate limiting:
- **Limit**: 100 requests per minute per tenant
- **Response**: 429 Too Many Requests with `retryAfter` in seconds

## Architecture Notes

### CQRS Handler Pattern

The DeviceManager uses a CQRS (Command Query Responsibility Segregation) pattern with plain ASP.NET Core dependency injection:

**Command Handlers**: Handle state-changing operations
```csharp
public class RegisterDeviceHandler
{
    private readonly IDeviceRepository _deviceRepository;

    public RegisterDeviceHandler(IDeviceRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<Result<RegisterDeviceResponse>> Handle(
        RegisterDeviceCommand command,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

**Query Handlers**: Handle read-only operations
```csharp
public class GetDevicesHandler
{
    private readonly IDeviceQueryRepository _queryRepository;

    // Similar pattern
}
```

All handlers are registered in DI:
```csharp
builder.Services.AddScoped<RegisterDeviceHandler>();
builder.Services.AddScoped<GetDevicesHandler>();
// etc.
```

## Public Endpoints (No Authentication Required)

The following endpoints are publicly accessible without API keys:

- **Scalar API Docs**: `/scalar/v1` - Interactive API documentation
- **OpenAPI Spec**: `/openapi/v1.json` - OpenAPI JSON specification
- **Health Checks**: `/health` - Service health status
- **Metrics**: `/metrics` - Prometheus metrics

All other API endpoints (`/api/*`) require authentication via API key or JWT.

## Troubleshooting

### API key error on documentation endpoints
```json
{"error":"missing_api_key","message":"API key is required in X-Api-Key header."}
```
**Solution**: This has been fixed. Documentation endpoints (`/scalar/v1`, `/openapi/v1.json`) are now publicly accessible without authentication. Restart your application to apply the changes.

### Docker not running
```
Error: Cannot connect to Docker daemon
```
**Solution**: Start Docker Desktop

### Port already in use
```
Error: Address already in use
```
**Solution**: Stop other services using the same ports or change ports in AppHost

### Database connection failed
```
Error: Connection refused (PostgreSQL)
```
**Solution**: Wait for PostgreSQL container to fully start (check Aspire Dashboard)

### NATS connection failed
```
Error: Connection refused (NATS)
```
**Solution**: Verify NATS container is running in Aspire Dashboard

## Next Steps

- **Add Database Migrations**: Run EF Core migrations to create tables
- **Configure Production JWT**: Set up Azure AD/Entra ID for production
- **Add More Services**: Uncomment BundleOrchestrator and TelemetryProcessor in AppHost
- **Deploy to AKS**: Use Helm charts in `src/DeviceManager/charts/`

## Resources

- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Scalar API Documentation](https://scalar.com/)
- [NATS JetStream Guide](https://docs.nats.io/nats-concepts/jetstream)
- [SignalBeam Architecture](./CLAUDE.md)
