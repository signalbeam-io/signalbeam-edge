# SignalBeam TelemetryProcessor Host

The TelemetryProcessor is a background service that consumes telemetry messages from NATS JetStream and processes device heartbeats, metrics, and status updates.

## Overview

This service acts as a **message consumer** rather than a traditional HTTP API. It:

- Consumes device heartbeats from NATS JetStream
- Processes device metrics (CPU, memory, disk usage)
- Monitors device status (online/offline)
- Stores telemetry data in TimescaleDB for time-series analysis
- Provides health checks and Prometheus metrics endpoints

## Architecture

### Components

1. **NATS Consumer Service** (Infrastructure Layer)
   - Connects to NATS JetStream
   - Creates and manages JetStream streams and consumers
   - Processes messages with retry logic and error handling
   - Consumes from two main subjects:
     - `signalbeam.devices.heartbeat.>` → Device heartbeats
     - `signalbeam.telemetry.metrics.>` → Device metrics

2. **Background Services** (Application Layer)
   - **DeviceStatusMonitor**: Checks for stale devices and marks them as offline
   - **MetricsAggregationService**: Placeholder for custom aggregation tasks (TimescaleDB handles most aggregation)

3. **Message Handlers** (Application Layer)
   - **DeviceHeartbeatMessageHandler**: Processes heartbeat messages
   - **DeviceMetricsMessageHandler**: Processes metrics messages

## Configuration

### Connection Strings

```json
{
  "ConnectionStrings": {
    "TelemetryDb": "Host=localhost;Database=signalbeam;Username=postgres;Password=postgres"
  }
}
```

### NATS Configuration

```json
{
  "NATS": {
    "Url": "nats://localhost:4222",
    "JetStream": {
      "Enabled": true,
      "StorageType": "File"
    },
    "Subjects": {
      "DeviceMetrics": "signalbeam.telemetry.metrics.>",
      "DeviceHeartbeats": "signalbeam.devices.heartbeat.>"
    },
    "Streams": {
      "DeviceMetrics": "DEVICE_METRICS",
      "DeviceHeartbeats": "DEVICE_HEARTBEATS"
    }
  }
}
```

### Background Services Configuration

```json
{
  "DeviceStatusMonitor": {
    "CheckInterval": "00:01:00",
    "HeartbeatThreshold": "00:02:00"
  },
  "MetricsAggregation": {
    "Enabled": true,
    "AggregationInterval": "00:05:00"
  }
}
```

## Running Locally

### With .NET Aspire (Recommended)

```bash
cd src/SignalBeam.AppHost
dotnet run
```

The Aspire dashboard will open at `http://localhost:15888` and start:
- PostgreSQL with TimescaleDB
- NATS with JetStream
- Valkey (Redis)
- All microservices including TelemetryProcessor

### Standalone

1. **Start dependencies:**
   ```bash
   # PostgreSQL
   docker run -d -p 5432:5432 -e POSTGRES_PASSWORD=postgres postgres:15

   # NATS with JetStream
   docker run -d -p 4222:4222 -p 8222:8222 nats:latest --jetstream
   ```

2. **Run the service:**
   ```bash
   cd src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Host
   dotnet run
   ```

## Health Checks

The service exposes health check endpoints via Aspire ServiceDefaults:

- `GET /health` - Overall health status
- `GET /health/live` - Liveness probe (is the process running?)
- `GET /health/ready` - Readiness probe (can it handle traffic?)

Health checks verify:
- PostgreSQL/TimescaleDB connectivity
- NATS connection status

## Metrics

Prometheus metrics are exposed at `/metrics` endpoint via OpenTelemetry:

- Standard ASP.NET Core metrics (requests, latency, etc.)
- Custom application metrics (message processing rates, errors, etc.)
- Runtime metrics (GC, thread pool, etc.)

## Deployment

### Docker

Build the image:
```bash
docker build -f src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Host/Dockerfile -t signalbeam/telemetry-processor:latest .
```

Run the container:
```bash
docker run -d \
  -p 8080:8080 \
  -e ConnectionStrings__TelemetryDb="Host=postgres;Database=signalbeam;Username=postgres;Password=postgres" \
  -e NATS__Url="nats://nats:4222" \
  signalbeam/telemetry-processor:latest
```

### Kubernetes

Deploy using Helm:
```bash
helm install telemetry-processor ./charts -n signalbeam
```

The service is designed to run as a Deployment with multiple replicas for high availability. Each replica will:
- Create a durable JetStream consumer
- Process messages independently
- Use consumer groups for load distribution

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment name | `Production` |
| `ASPNETCORE_URLS` | HTTP listening URL | `http://+:8080` |
| `ConnectionStrings__TelemetryDb` | PostgreSQL connection string | - |
| `NATS__Url` | NATS server URL | `nats://localhost:4222` |
| `DeviceStatusMonitor__CheckInterval` | How often to check for stale devices | `00:01:00` |
| `DeviceStatusMonitor__HeartbeatThreshold` | Heartbeat timeout threshold | `00:02:00` |

## Monitoring

### Logs

The service uses **Serilog** for structured logging:

- Console sink (development)
- Grafana Loki sink (production)
- Enriched with: correlation ID, machine name, thread ID

### Traces

Distributed tracing with **OpenTelemetry**:

- OTLP exporter to Tempo/Jaeger
- Traces all message processing
- Includes database operations via EF Core instrumentation

### Dashboards

Recommended Grafana dashboards:

1. **Service Health**: Health check status, uptime, errors
2. **Message Processing**: Messages consumed, processing rate, errors
3. **Database Performance**: Query performance, connection pool
4. **Device Telemetry**: Active devices, heartbeat rates, offline devices

## NATS JetStream Details

### Streams

The service automatically creates JetStream streams on startup:

**DEVICE_METRICS Stream:**
- Subject: `signalbeam.telemetry.metrics.>`
- Retention: 30 days
- Storage: File

**DEVICE_HEARTBEATS Stream:**
- Subject: `signalbeam.devices.heartbeat.>`
- Retention: 30 days
- Storage: File

### Consumers

The service creates durable consumers:

**telemetry-processor-metrics:**
- Stream: DEVICE_METRICS
- Ack Policy: Explicit
- Max Deliver: 3 attempts
- Ack Wait: 30 seconds

**telemetry-processor-heartbeats:**
- Stream: DEVICE_HEARTBEATS
- Ack Policy: Explicit
- Max Deliver: 3 attempts
- Ack Wait: 30 seconds

### Message Processing

Messages are processed with:
- **Batching**: Fetch up to 10 messages at a time
- **Error Handling**: NAK with 5-second delay on processing errors
- **Dead Letter**: Messages exceeding max delivery are automatically moved to dead letter queue by NATS
- **Graceful Shutdown**: Consumers complete in-flight messages before stopping

## Troubleshooting

### NATS Connection Issues

Check NATS connectivity:
```bash
# View NATS logs
docker logs <nats-container>

# Test connection
nats-cli --server nats://localhost:4222 server check connection
```

### Database Issues

Check PostgreSQL connectivity:
```bash
# Connect to database
psql -h localhost -U postgres -d signalbeam

# Verify TimescaleDB extension
SELECT extname, extversion FROM pg_extension WHERE extname = 'timescaledb';
```

### Message Processing Delays

1. Check consumer lag in NATS:
   ```bash
   nats consumer info DEVICE_METRICS telemetry-processor-metrics
   ```

2. Scale up replicas:
   ```bash
   kubectl scale deployment telemetry-processor --replicas=3
   ```

3. Check resource usage (CPU, memory)

## Related Projects

- **Application Layer**: `SignalBeam.TelemetryProcessor.Application` - Business logic and message handlers
- **Infrastructure Layer**: `SignalBeam.TelemetryProcessor.Infrastructure` - NATS integration, repositories
- **DeviceManager**: Publishes device events consumed by this service
- **EdgeAgent**: Publishes heartbeats and metrics consumed by this service

## Development

### Adding New Message Types

1. Define message model in Application layer:
   ```csharp
   public record NewMessage(Guid DeviceId, DateTime Timestamp, ...);
   ```

2. Create message handler:
   ```csharp
   public class NewMessageHandler
   {
       public async Task Handle(NewMessage message, CancellationToken ct) { ... }
   }
   ```

3. Register handler in Infrastructure DependencyInjection.cs

4. Add consumer in NatsConsumerService

### Running Tests

```bash
# Unit tests
dotnet test tests/SignalBeam.TelemetryProcessor.Tests.Unit

# Integration tests (requires Testcontainers)
dotnet test tests/SignalBeam.TelemetryProcessor.Tests.Integration
```

## License

Copyright © 2024 SignalBeam
