# Running SignalBeam with .NET Aspire

This guide shows how to run the entire SignalBeam platform using .NET Aspire for local development.

## Prerequisites

- .NET 10.0 SDK
- Docker Desktop (running)
- .NET Aspire workload: `dotnet workload install aspire`

## Architecture with Aspire

```
┌─────────────────────────────────────────────────────────────┐
│                    .NET Aspire AppHost                      │
│                    (Orchestration Layer)                     │
└─────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
        ▼                     ▼                     ▼
┌─────────────┐      ┌─────────────┐      ┌─────────────┐
│Infrastructure│      │  Services   │      │   Gateway   │
└─────────────┘      └─────────────┘      └─────────────┘
        │                     │                     │
        ├── PostgreSQL        ├── DeviceManager    └── API Gateway
        ├── Valkey (Redis)    ├── BundleOrch.          (YARP)
        ├── NATS + JetStream  ├── Telemetry          Port 8080
        └── Azurite (Blob)    └── EdgeAgent              │
                                   Simulator         Routes to:
                                                     - /api/devices/* → device-manager
                                                     - /api/bundles/* → bundle-orchestrator
                                                     - /api/rollouts/* → bundle-orchestrator
                                                     - /api/groups/* → device-manager
                                                     - /api/heartbeat/* → telemetry-processor
```

### API Gateway (YARP)

The API Gateway provides a single entry point for all microservices:

- **Technology:** YARP (Yet Another Reverse Proxy) from Microsoft
- **Port:** 8080 (fixed)
- **Service Discovery:** Uses Aspire's built-in service discovery to route requests
- **CORS:** Configured to allow frontend requests from Vite dev server
- **Routes:** Automatically routes based on URL path patterns

The gateway eliminates the need to know individual service ports and provides:
- Single endpoint for frontend and edge agents
- Centralized CORS configuration
- Request routing based on path patterns
- Health check aggregation

## Running with Aspire

### Option 1: Run from AppHost (Recommended)

```bash
cd src/SignalBeam.AppHost
dotnet run
```

This will:
1. Start all infrastructure containers (PostgreSQL, Valkey, NATS, Azurite)
2. Start all microservices (DeviceManager, BundleOrchestrator, TelemetryProcessor)
3. Start the API Gateway on port 8080
4. Start the Edge Agent Simulator as a fleet of 20 devices (see below)
5. Open the Aspire Dashboard at `http://localhost:15888`

### Simulated fleet

The simulator runs 20 devices with seed 42. The seed fixes the device IDs, names, groups, tags,
start offsets, timing jitter and metric values, so every run produces the same fleet and the same
traffic shape. Devices are split across the `lab` and `prod-eu` groups and tagged
`environment=<group>` and `hardware=rpi|x86`, so tag queries such as `hardware=rpi` have data.
Change the size or seed with `dotnet run -- --Simulator:FleetSize=5 --Simulator:Seed=7`.

Fleet setup registers devices at DeviceManager's registration limit (10 per minute per IP), so
20 devices take about two minutes to come up; setup waits out 429s. At steady state each device
sends a heartbeat and a metrics report every 30s, ~80 DeviceManager calls a minute for the default
fleet, against a per-tenant limit of 100 that the UI and other callers in the dev tenant share.
Fleet devices report bundle state only when their desired bundle changes, so a rollout adds a
short burst rather than a steady load. Fleets much larger than 20 will see 429s at steady state;
raise the intervals (`--heartbeat-interval-seconds`, `--metrics-interval-seconds`) to compensate.

Outside Aspire: `dotnet run --project src/EdgeAgent/SignalBeam.EdgeAgent.Simulator -- --fleet-size 20 --seed 42`
(plus `--device-manager-url`, `--api-key`, `--tenant-id` or the `SIM_*` variables).
To inject failures (devices going offline, resource spikes, failed deploys), see
[simulator-scenarios.md](simulator-scenarios.md). The AppHost also gives the simulator the NATS URL,
so heartbeats and metrics reach TelemetryProcessor the way a real agent's do.

`--fleet-size 1` (the default) is the original single-device mode, including `--use-device-auth`
and `--use-mtls`. With `--seed` it also gets fleet device 1's ID and name and the +/-10% jitter.

### Option 2: Run from Visual Studio / Rider

1. Open `SignalBeam.sln`
2. Set `SignalBeam.AppHost` as the startup project
3. Press F5 or click Run

## Aspire Dashboard

The Aspire Dashboard provides:

- **Resources Tab**: View all running services and containers
- **Console Logs**: Real-time logs from all services
- **Structured Logs**: Filterable, searchable logs
- **Traces**: Distributed tracing across services
- **Metrics**: Real-time performance metrics
- **Environment**: Inspect environment variables

Access at: `http://localhost:15888`

## Telemetry Pipeline (OTel Collector)

Services don't send telemetry to the dashboard directly. An OpenTelemetry Collector
(`otel-collector` resource, `otel/opentelemetry-collector-contrib`) sits in between:

```
services ──OTLP──► otel-collector ──► Aspire dashboard        (traces, metrics, logs)
                                  └─► .tracebeam/otlp/traces.jsonl   (traces)
```

- Config: `src/SignalBeam.AppHost/otel-collector.yaml`. Routing is done by
  `CommunityToolkit.Aspire.Hosting.OpenTelemetryCollector` (`WithAppForwarding()`), so every
  project's `OTEL_EXPORTER_OTLP_ENDPOINT` points at the collector automatically.
- `.tracebeam/otlp/traces.jsonl` holds every span as OTLP/JSON, one export batch per line
  (rotated at 50 MB, 10 backups, git-ignored). It is the machine-readable record of how the
  system behaves, for tools like [tracebeam](https://github.com/makigjuro/tracebeam) and coding
  agents (see #472). The file is created when the first spans arrive. With rotation it uses at
  most about 550 MB.
- **Treat the file as sensitive.** Spans can contain URLs with query strings, SQL text, tenant and
  device IDs, and exception messages. Don't attach it to issues or share it outside your machine.
- Quick look without extra tools:

  ```bash
  # spans per service and route
  jq -r '.resourceSpans[] | (.resource.attributes[] | select(.key=="service.name").value.stringValue) as $s
         | .scopeSpans[].spans[] | select(.kind==2)
         | "\($s) \(.attributes[] | select(.key=="http.route").value.stringValue)"' \
     .tracebeam/otlp/traces.jsonl | sort | uniq -c
  ```

- **What's traced:** inbound HTTP (`http.route`), outgoing `HttpClient` calls, and every Postgres
  command via Npgsql's built-in `Npgsql` ActivitySource (EF Core and raw ADO.NET commands alike). Database spans
  are named after their summary, e.g. `SELECT device_manager.devices`, and carry `db.system.name`,
  `db.namespace`, `db.operation.name`, `db.collection.name`, `db.query.summary` and `db.query.text`
  (parameters appear as placeholders; only constants written in code, such as EF-inlined
  literals, appear inline). The summary comes from
  `ServiceDefaults/Observability/DatabaseSpanProcessor`, because Npgsql names every plain-SQL span
  `postgresql`. Spans from background workers (e.g. TelemetryProcessor's monitors) have no parent request.
- **NATS** (Core and JetStream) is traced via NATS.Net's built-in `NATS.Net` ActivitySource. Publishing
  injects `traceparent` into the message headers and receiving continues it, so a consumer's work
  joins the trace of whoever published. TelemetryProcessor wraps each JetStream message in a
  `process <filter subject>` span (e.g. `process signalbeam.devices.heartbeat.>`), so the handler's
  database writes and follow-up publishes nest under it. JetStream protocol traffic (`$JS.API.*`
  fetch/API calls, `_INBOX.*` replies) is filtered out by `ServiceDefaults/Observability/NatsTracing`, applied in `ConfigureOpenTelemetry`.
  To see a cross-service NATS trace locally, publish with a trace header, e.g.
  `nats pub signalbeam.devices.heartbeat.<deviceId> '<json>' -H "traceparent:00-<32 hex>-<16 hex>-01"`.
  The simulator itself uses HTTP. Each simulated heartbeat, metrics report and desired-state poll
  is its own trace, with a `simulate <action>` root span (source `SignalBeam.Simulator`) tagged
  with the device id, name and environment.
- To add another destination (e.g. Jaeger or Tempo), add an exporter to the collector config.
  No service changes are needed.

## Service Endpoints

When running with Aspire, **all API requests** should go through the API Gateway:

| Service | Endpoint | Description |
|---------|----------|-------------|
| **API Gateway** | `http://localhost:8080` | **Single entry point for all APIs** |
| Aspire Dashboard | `http://localhost:15888` | Monitoring, logs, traces, metrics |
| DeviceManager | Service Discovery Only | Not directly accessible |
| BundleOrchestrator | Service Discovery Only | Not directly accessible |
| TelemetryProcessor | Service Discovery Only | Not directly accessible |
| IdentityManager | Service Discovery Only | Not directly accessible (`/api/auth`, `/api/subscriptions`, `/api/teams`) |
| PostgreSQL | Dynamic port | Check Aspire Dashboard for connection |
| Valkey | Dynamic port | Check Aspire Dashboard for connection |
| NATS | Dynamic port | Check Aspire Dashboard for connection |

### API Routes via Gateway

All API requests are routed through `http://localhost:8080`:

```bash
# Device Management
GET    http://localhost:8080/api/devices
POST   http://localhost:8080/api/devices
GET    http://localhost:8080/api/devices/{id}

# Bundle Management
GET    http://localhost:8080/api/bundles
POST   http://localhost:8080/api/bundles
GET    http://localhost:8080/api/bundles/{id}

# Device Groups
GET    http://localhost:8080/api/groups
POST   http://localhost:8080/api/groups

# Rollouts
GET    http://localhost:8080/api/rollouts
POST   http://localhost:8080/api/rollouts

# Health & Heartbeat
POST   http://localhost:8080/api/heartbeat
GET    http://localhost:8080/health
```

**Important**: The gateway uses Aspire's service discovery to automatically route requests to the correct microservice. You never need to know individual service ports!

## Frontend Configuration

Update your frontend `.env.development`:

```env
VITE_API_URL=http://localhost:8080
```

Then run:

```bash
cd web
npm run dev
```

The frontend will now communicate with all microservices through the API Gateway.

## Service Discovery

Aspire provides automatic service discovery:

- Services reference each other by name (e.g., `http://device-manager`)
- Aspire resolves names to actual endpoints at runtime
- No need to manage ports manually
- Works across all environments (local, Docker, Kubernetes)

## Hot Reload

Changes to your code are automatically detected:

1. Make changes to any service
2. Save the file
3. Aspire rebuilds and restarts the service
4. Watch logs in the Aspire Dashboard

## Stopping Services

**Graceful shutdown:**
```bash
# Press Ctrl+C in the terminal running AppHost
```

**Force stop:**
```bash
docker ps  # Find container IDs
docker stop <container-id>
```

## Debugging

### Debug a specific service:

1. In Visual Studio/Rider, attach debugger to the service process
2. Or add breakpoints and run AppHost in debug mode
3. Aspire will launch services in debug mode

### View logs:

- Real-time: Aspire Dashboard → Console Logs
- Structured: Aspire Dashboard → Structured Logs tab
- Filter by service, log level, or search text

## Database Migrations

Run migrations when starting for the first time:

```bash
# Aspire auto-applies migrations on startup
# Or manually run:
cd src/DeviceManager/SignalBeam.DeviceManager.Infrastructure
dotnet ef database update

cd src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Infrastructure
dotnet ef database update
```

## Common Issues

### Port conflicts

If port 8080 is already in use:
1. Edit `src/SignalBeam.AppHost/Program.cs`
2. Change `.WithHttpEndpoint(port: 8080, name: "http")` to another port
3. Update frontend `VITE_API_URL`

### Containers not starting

1. Check Docker Desktop is running
2. Check for port conflicts: `docker ps`
3. Clean up old containers: `docker system prune`

### Service won't start

1. Check Aspire Dashboard logs for errors
2. Verify dependencies are running (PostgreSQL, etc.)
3. Check for missing environment variables

### Zitadel exits with "partitioned tables cannot be unlogged"

Zitadel v2.66.3 can't initialise on Postgres 18, and Aspire 13.6 defaults to 18.x. The AppHost
runs `timescale/timescaledb:2.32.1-pg17-oss` (Postgres 17 + TimescaleDB, which TelemetryProcessor's
migrations need) until Zitadel is upgraded (#483). If an old Postgres 18 container is still around,
remove it (`docker rm -f postgres-<suffix> zitadel-<suffix>`) so both are recreated. The local
Postgres has no data volume, so this **wipes your local SignalBeam and Zitadel data**.

Whenever the Postgres container is recreated (e.g. after an image change), also remove the
`zitadel-<suffix>` container. Otherwise Zitadel keeps running against an empty database and
`zitadel-setup` fails.

### Services stay "Waiting" / no traces in the dashboard

All projects wait for `otel-collector` to become healthy. If it never starts, check that its
image can be pulled: `docker pull otel/opentelemetry-collector-contrib:0.164.0`.

## Next Steps

- Explore the Aspire Dashboard
- Check distributed traces between services
- Monitor metrics in real-time
- Test the API Gateway routing

## Production Deployment

For production, Aspire can:
- Generate Kubernetes manifests
- Create Docker Compose files
- Export to Azure Container Apps
- Deploy to any container orchestrator

See: https://learn.microsoft.com/en-us/dotnet/aspire/deployment/overview
