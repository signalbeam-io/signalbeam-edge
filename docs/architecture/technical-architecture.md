# Technical Architecture

This document provides a detailed technical overview of the SignalBeam Edge platform, including system design, component interactions, data flow, and architectural decisions.

## Table of Contents

- [System Overview](#system-overview)
- [Architectural Patterns](#architectural-patterns)
- [Component Architecture](#component-architecture)
- [Data Architecture](#data-architecture)
- [Communication Patterns](#communication-patterns)
- [Security Architecture](#security-architecture)
- [Deployment Architecture](#deployment-architecture)

## System Overview

SignalBeam Edge follows a **microservices architecture** with clear service boundaries, using **Domain-Driven Design (DDD)** principles and **CQRS** pattern for complex workflows.

### High-Level Architecture

```mermaid
graph TB
    subgraph WebUI["Web UI (React)"]
        UI[Frontend Application<br/>http://localhost:5173]
    end

    subgraph Backend["Backend Services"]
        DM[DeviceManager<br/>Port 5001<br/><br/>• Devices<br/>• Groups<br/>• Tags]
        BO[BundleOrchestrator<br/>Port 5002<br/><br/>• Bundles<br/>• Versions<br/>• Rollouts]
        TP[TelemetryProcessor<br/>Port 5003<br/><br/>• Metrics<br/>• Heartbeat]
    end

    subgraph Infrastructure["Infrastructure Layer"]
        DB[(PostgreSQL +<br/>TimescaleDB)]
        Cache[(Valkey<br/>Redis-compatible)]
        Blob[Azure Blob Storage<br/>Azurite local]
    end

    subgraph Messaging["Message Broker"]
        NATS[NATS + JetStream<br/><br/>• Event Streaming<br/>• Pub/Sub]
    end

    subgraph Edge["Edge Devices"]
        Agent[Edge Agent<br/><br/>• Heartbeat sender<br/>• Status reporter<br/>• Container reconciler<br/>• Docker/Podman]
    end

    UI -->|HTTPS/REST| DM
    UI -->|HTTPS/REST| BO
    UI -->|HTTPS/REST| TP

    DM -.->|Read/Write| DB
    BO -.->|Read/Write| DB
    TP -.->|Read/Write| DB

    DM -.->|Cache| Cache
    BO -.->|Cache| Cache
    TP -.->|Cache| Cache

    BO -.->|Store Artifacts| Blob

    DM -->|Publish Events| NATS
    BO -->|Publish Events| NATS
    TP -->|Subscribe Events| NATS

    Agent -->|HTTP/REST| DM
    Agent -->|HTTP/REST| BO
    Agent -->|Publish Events| NATS

    style UI fill:#e1f5ff
    style DM fill:#c8e6c9
    style BO fill:#c8e6c9
    style TP fill:#c8e6c9
    style DB fill:#fff9c4
    style Cache fill:#fff9c4
    style Blob fill:#fff9c4
    style NATS fill:#ffe0b2
    style Agent fill:#f8bbd0
```

## Architectural Patterns

### 1. Hexagonal Architecture (Ports & Adapters)

Each microservice follows the **Hexagonal Architecture** pattern:

```mermaid
graph TD
    subgraph Service["SignalBeam Microservice"]
        subgraph Host["Host Layer (Web API)"]
            Endpoints[Minimal API Endpoints]
            Middleware[Middleware Pipeline]
            DI[Dependency Injection]
        end

        subgraph Application["Application Layer (CQRS)"]
            Commands[Command Handlers]
            Queries[Query Handlers]
            Services[Application Services]
            DTOs[DTOs & Validators]
        end

        subgraph Domain["Domain Layer (Business Logic)"]
            Entities[Entities & Aggregates]
            ValueObjects[Value Objects]
            Events[Domain Events]
            Rules[Business Rules]
        end

        subgraph Infrastructure["Infrastructure Layer (Adapters)"]
            EF[EF Core DbContext]
            Repos[Repository Implementations]
            Clients[External Service Clients]
            Publishers[Message Publishers]
        end

        Host --> Application
        Application --> Domain
        Domain --> Infrastructure
    end

    External[External Systems] -.->|Adapters| Infrastructure
    HTTP[HTTP Requests] -->|Entry Point| Host

    style Host fill:#e1f5ff
    style Application fill:#c8e6c9
    style Domain fill:#fff9c4
    style Infrastructure fill:#ffe0b2
```

**Benefits**:
- Clear separation of concerns
- Testable business logic independent of infrastructure
- Easy to swap implementations (e.g., databases, message brokers)
- Technology-agnostic domain layer

### 2. CQRS (Command Query Responsibility Segregation)

Commands and Queries are separated:

**Commands** (Write Operations):
```csharp
// Command: Change state
public record CreateRolloutCommand(
    Guid BundleId,
    string Version,
    string TargetType,
    List<Guid> TargetIds);

// Handler: Executes business logic
public class CreateRolloutHandler
{
    public async Task<Result<RolloutDto>> Handle(
        CreateRolloutCommand command,
        CancellationToken ct)
    {
        // Business logic
        // Persist changes
        // Publish domain events
    }
}
```

**Queries** (Read Operations):
```csharp
// Query: Read data
public record GetRolloutsQuery(
    string? BundleId,
    int Page,
    int PageSize);

// Handler: Optimized for reading
public class GetRolloutsHandler
{
    public async Task<Result<PaginatedResponse>> Handle(
        GetRolloutsQuery query,
        CancellationToken ct)
    {
        // Optimized read query
        // Can use Dapper for performance
    }
}
```

**Benefits**:
- Optimized read and write paths
- Simpler business logic
- Better scalability
- Clear intent in code

### 3. Result Pattern

Instead of throwing exceptions for business logic failures:

```csharp
public record Result<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public Error? Error { get; init; }
}

// Usage
var result = await handler.Handle(command, ct);

if (result.IsSuccess)
{
    return Results.Ok(result.Value);
}
else
{
    return Results.BadRequest(new
    {
        error = result.Error.Code,
        message = result.Error.Message
    });
}
```

**Benefits**:
- Explicit error handling
- No exceptions for business logic failures
- Better performance
- Clearer code intent

### 4. Event-Driven Architecture

Domain events are published for state changes and processed asynchronously via NATS:

```csharp
// Domain Event
public record DeviceRegisteredEvent(
    Guid DeviceId,
    Guid TenantId,
    string Name);

// Publisher (in Infrastructure)
await _messagePublisher.PublishAsync(
    "signalbeam.devices.registered",
    deviceRegisteredEvent,
    ct);

// Subscriber (in another service)
public class DeviceRegisteredHandler
{
    public async Task Handle(
        DeviceRegisteredEvent @event,
        CancellationToken ct)
    {
        // Handle side effects
    }
}
```

## Component Architecture

### DeviceManager Service

**Responsibilities**:
- Device registration and authentication
- Device lifecycle management
- Grouping and tagging
- Heartbeat ingestion
- Desired state management

**Key Endpoints**:
- `POST /api/devices` - Register device
- `GET /api/devices` - List devices (paginated)
- `GET /api/devices/{id}` - Get device details
- `POST /api/devices/{id}/heartbeat` - Update heartbeat
- `GET /api/groups` - List device groups

**Database Tables**:
- `Devices` - Device identity and metadata
- `DeviceGroups` - Logical groupings
- `DeviceTags` - Tagging system
- `DeviceHeartbeats` - Health metrics (TimescaleDB)
- `DeviceEvents` - Activity log

### BundleOrchestrator Service

**Responsibilities**:
- Bundle definition and versioning
- Bundle assignment to devices/groups
- Rollout orchestration and tracking
- Container manifest management

**Key Endpoints**:
- `GET /api/bundles` - List bundles
- `POST /api/bundles` - Create bundle
- `POST /api/bundles/{id}/versions` - Create version
- `POST /api/rollouts` - Create rollout
- `GET /api/rollouts` - List rollouts (with pagination)
- `GET /api/rollouts/{id}` - Get rollout details
- `GET /api/rollouts/{id}/devices` - Get device-level status
- `POST /api/rollouts/{id}/cancel` - Cancel rollout

**Database Tables**:
- `AppBundles` - Bundle definitions
- `AppBundleVersions` - Version history
- `DeviceDesiredState` - Target configuration per device
- `RolloutStatus` - Device-level rollout tracking

**Storage**:
- Azure Blob Storage for bundle manifests/artifacts

### TelemetryProcessor Service

**Responsibilities**:
- Process device metrics
- Aggregate telemetry data
- Store time-series data in TimescaleDB

**Message Subscriptions**:
- `signalbeam.devices.heartbeat` - Device heartbeat events
- `signalbeam.devices.metrics` - Device metrics streams

**Database Tables**:
- `DeviceMetrics` (TimescaleDB hypertable) - Time-series metrics
- `DeviceReportedState` - Current state snapshot

### Edge Agent

**Responsibilities**:
- Device registration
- Periodic heartbeat transmission
- Fetch desired bundle configuration
- Reconcile containers (Docker/Podman)
- Report deployment status

**Configuration**:
```json
{
  "ApiBaseUrl": "https://api.signalbeam.io",
  "DeviceId": "device-123",
  "TenantId": "tenant-456",
  "ApiKey": "...",
  "HeartbeatIntervalSeconds": 30,
  "ReconciliationIntervalSeconds": 60
}
```

## Data Architecture

### Database Schema

**PostgreSQL** is the primary data store with **TimescaleDB** extension for time-series data.

#### Devices Context (DeviceManager)

```sql
-- Devices
CREATE TABLE devices (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    name VARCHAR(200) NOT NULL,
    status VARCHAR(50) NOT NULL,
    last_seen_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL
);

-- Device Groups
CREATE TABLE device_groups (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    name VARCHAR(200) NOT NULL,
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL
);

-- Device Heartbeats (TimescaleDB hypertable)
CREATE TABLE device_heartbeats (
    id UUID PRIMARY KEY,
    device_id UUID NOT NULL,
    timestamp TIMESTAMPTZ NOT NULL,
    cpu_usage DOUBLE PRECISION,
    memory_usage DOUBLE PRECISION,
    disk_usage DOUBLE PRECISION
);

SELECT create_hypertable('device_heartbeats', 'timestamp');
```

#### Bundles Context (BundleOrchestrator)

```sql
-- App Bundles
CREATE TABLE app_bundles (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    name VARCHAR(200) NOT NULL,
    description TEXT,
    current_version VARCHAR(50),
    created_at TIMESTAMPTZ NOT NULL
);

-- Bundle Versions
CREATE TABLE app_bundle_versions (
    id UUID PRIMARY KEY,
    bundle_id UUID NOT NULL REFERENCES app_bundles(id),
    version VARCHAR(50) NOT NULL,
    containers JSONB NOT NULL,
    is_active BOOLEAN NOT NULL,
    created_at TIMESTAMPTZ NOT NULL
);

-- Rollout Status
CREATE TABLE rollout_status (
    id UUID PRIMARY KEY,
    rollout_id UUID NOT NULL,
    bundle_id UUID NOT NULL,
    bundle_version VARCHAR(50) NOT NULL,
    device_id UUID NOT NULL,
    status VARCHAR(50) NOT NULL,
    started_at TIMESTAMPTZ NOT NULL,
    completed_at TIMESTAMPTZ,
    error_message TEXT,
    retry_count INT NOT NULL DEFAULT 0
);

CREATE INDEX idx_rollout_status_rollout_id ON rollout_status(rollout_id);
CREATE INDEX idx_rollout_status_device_id ON rollout_status(device_id);
```

### Caching Strategy (Valkey/Redis)

**Cache Keys**:
- `device:{deviceId}` - Device details (TTL: 5 minutes)
- `device:{deviceId}:heartbeat` - Latest heartbeat (TTL: 1 minute)
- `bundle:{bundleId}` - Bundle details (TTL: 10 minutes)
- `rollout:{rolloutId}` - Rollout summary (TTL: 10 seconds for active rollouts)

**Cache Invalidation**:
- On device update → invalidate `device:{deviceId}`
- On heartbeat → update `device:{deviceId}:heartbeat`
- On bundle update → invalidate `bundle:{bundleId}`
- On rollout status change → invalidate `rollout:{rolloutId}`

## Communication Patterns

### 1. Synchronous Communication (REST)

**Frontend ↔ Backend**:
- HTTP/HTTPS
- REST APIs with JSON payloads
- JWT authentication (users) / API Key (devices)

**Request Flow**:
```
Web UI → DeviceManager API → PostgreSQL
                           → Valkey (cache)
```

### 2. Asynchronous Communication (NATS)

**Event Publishing**:
```csharp
// Service A publishes event
await _publisher.PublishAsync(
    "signalbeam.devices.registered",
    new DeviceRegisteredEvent(...),
    ct);
```

**Event Subscription**:
```csharp
// Service B subscribes to event
var subscription = await _jetStream.PushSubscribeAsync<DeviceRegisteredEvent>(
    "signalbeam.devices.registered");

await foreach (var msg in subscription.Msgs.ReadAllAsync(ct))
{
    await HandleEvent(msg.Data);
    await msg.AckAsync();
}
```

**NATS Subject Hierarchy**:

```mermaid
graph TD
    Root[signalbeam.*]

    Root --> Devices[devices.*]
    Root --> Bundles[bundles.*]
    Root --> Telemetry[telemetry.*]

    Devices --> DevReg[registered]
    Devices --> DevHB[heartbeat.deviceId]
    Devices --> DevStatus[status.deviceId]
    Devices --> DevEvents[events.eventType]

    Bundles --> BunAssigned[assigned]
    Bundles --> BunRollouts[rollouts.rolloutId]
    Bundles --> BunArtifacts[artifacts.uploaded]

    Telemetry --> TelMetrics[metrics.deviceId]
    Telemetry --> TelLogs[logs.deviceId]

    DevReg -.->|JetStream| RegNote[Device registration events]
    DevHB -.->|Core NATS| HBNote[Heartbeat stream ephemeral]
    DevStatus -.->|JetStream| StatusNote[Status changes]

    BunAssigned -.->|JetStream| AssignNote[Bundle assignments]
    BunRollouts -.->|JetStream| RollNote[Rollout progress]

    TelMetrics -.->|Core NATS| MetNote[High-frequency metrics]
    TelLogs -.->|Core NATS| LogNote[Log streaming future]

    style Root fill:#e1f5ff
    style Devices fill:#c8e6c9
    style Bundles fill:#fff9c4
    style Telemetry fill:#ffe0b2
```

## Security Architecture

### Authentication

#### Device Authentication (API Key)
```http
GET /api/devices/me HTTP/1.1
Host: api.signalbeam.io
X-API-Key: sk_live_...
X-Tenant-Id: tenant-123
```

#### User Authentication (JWT)
```http
GET /api/devices HTTP/1.1
Host: api.signalbeam.io
Authorization: Bearer eyJhbGciOiJSUzI1NiIs...
```

### Authorization

**Tenant Isolation**:
- Every request includes `tenantId`
- Database queries filtered by tenant
- Multi-tenancy enforced at application level

**Role-Based Access Control** (Future):
- Admin: Full access
- Operator: Manage devices and bundles
- Viewer: Read-only access

### Data Protection

- **In Transit**: TLS 1.3 for all HTTP traffic
- **At Rest**: Database encryption via PostgreSQL (future)
- **Secrets**: Azure Key Vault / environment variables
- **API Keys**: Hashed and salted in database

## Deployment Architecture

### Local Development (.NET Aspire)

```mermaid
graph TB
    subgraph AspireHost[".NET Aspire AppHost"]
        Dashboard[Aspire Dashboard<br/>http://localhost:15888<br/><br/>• Logs & Traces<br/>• Metrics<br/>• Service Discovery]
    end

    subgraph Infrastructure["Infrastructure Containers"]
        PG[PostgreSQL<br/>+ TimescaleDB<br/>Port: 5432]
        VK[Valkey<br/>Redis-compatible<br/>Port: 6379]
        NATS[NATS + JetStream<br/>Port: 4222]
        AZ[Azurite<br/>Azure Storage Emulator<br/>Port: 10000]
    end

    subgraph Services["Backend Services dotnet run"]
        DM[DeviceManager<br/>Port: 5001]
        BO[BundleOrchestrator<br/>Port: 5002]
        TP[TelemetryProcessor<br/>Port: 5003]
    end

    subgraph Frontend["Frontend"]
        UI[React Web UI<br/>Vite Dev Server<br/>Port: 5173]
    end

    AspireHost -->|Orchestrates| Infrastructure
    AspireHost -->|Orchestrates| Services
    AspireHost -->|Monitors| Dashboard

    Services -->|Connect| Infrastructure
    UI -->|HTTP/REST| Services

    style Dashboard fill:#e1f5ff
    style PG fill:#fff9c4
    style VK fill:#fff9c4
    style NATS fill:#ffe0b2
    style AZ fill:#fff9c4
    style DM fill:#c8e6c9
    style BO fill:#c8e6c9
    style TP fill:#c8e6c9
    style UI fill:#f8bbd0
```

### Production Deployment (Kubernetes)

```mermaid
graph TB
    Internet([Internet Traffic])

    subgraph AKS["Azure Kubernetes Service (AKS)"]
        subgraph Ingress["Ingress Layer"]
            LB[NGINX Ingress<br/>or App Gateway<br/><br/>• TLS Termination<br/>• Rate Limiting<br/>• Load Balancing]
        end

        subgraph Mesh["Service Mesh - Cilium"]
            SM[eBPF-based Networking<br/><br/>• mTLS<br/>• Traffic Management<br/>• Observability]
        end

        subgraph Pods["Microservices Pods"]
            DM1[DeviceManager<br/>Replica 1]
            DM2[DeviceManager<br/>Replica 2]
            DM3[DeviceManager<br/>Replica 3]

            BO1[BundleOrchestrator<br/>Replica 1]
            BO2[BundleOrchestrator<br/>Replica 2]
            BO3[BundleOrchestrator<br/>Replica 3]

            TP1[TelemetryProcessor<br/>Replica 1]
            TP2[TelemetryProcessor<br/>Replica 2]
            TP3[TelemetryProcessor<br/>Replica 3]
        end

        Internet --> LB
        LB --> SM
        SM --> Pods
    end

    subgraph Azure["Azure Managed Services"]
        PSQL[(Azure Database<br/>for PostgreSQL<br/>+ TimescaleDB)]
        Redis[(Azure Cache<br/>for Redis<br/>or Valkey)]
        Blob[Azure Blob<br/>Storage]
    end

    subgraph SelfHosted["Self-Hosted in AKS"]
        NATSCluster[NATS Cluster<br/>3 nodes<br/>+ JetStream]
    end

    subgraph Observability["Observability Stack"]
        Prom[Prometheus]
        Grafana[Grafana]
        Loki[Loki]
        Tempo[Tempo]
    end

    Pods -.->|Read/Write| PSQL
    Pods -.->|Cache| Redis
    Pods -.->|Store Artifacts| Blob
    Pods -->|Pub/Sub| NATSCluster

    Pods -.->|Metrics| Prom
    Pods -.->|Logs| Loki
    Pods -.->|Traces| Tempo
    Prom --> Grafana
    Loki --> Grafana
    Tempo --> Grafana

    style LB fill:#e1f5ff
    style SM fill:#c8e6c9
    style DM1 fill:#c8e6c9
    style DM2 fill:#c8e6c9
    style DM3 fill:#c8e6c9
    style BO1 fill:#c8e6c9
    style BO2 fill:#c8e6c9
    style BO3 fill:#c8e6c9
    style TP1 fill:#c8e6c9
    style TP2 fill:#c8e6c9
    style TP3 fill:#c8e6c9
    style PSQL fill:#fff9c4
    style Redis fill:#fff9c4
    style Blob fill:#fff9c4
    style NATSCluster fill:#ffe0b2
    style Prom fill:#f8bbd0
    style Grafana fill:#f8bbd0
    style Loki fill:#f8bbd0
    style Tempo fill:#f8bbd0
```

**Infrastructure as Code**:
- Terraform for cloud resources
- Helm charts for Kubernetes deployments
- ArgoCD for GitOps

## Observability

### Logging (Serilog + Loki)

Structured logs shipped to Grafana Loki:
```csharp
Log.Information("Device {DeviceId} registered by tenant {TenantId}",
    deviceId, tenantId);
```

### Metrics (Prometheus)

Exposed at `/metrics` endpoint:
- Request rate, latency, errors (RED metrics)
- Business metrics (devices registered, rollouts completed)
- Infrastructure metrics (DB connections, cache hit rate)

### Tracing (OpenTelemetry + Tempo)

Distributed traces across services:
```
HTTP POST /api/rollouts
  ├─ BundleOrchestrator.CreateRollout
  │   ├─ Database: INSERT rollout_status
  │   └─ NATS: Publish rollout.created
  └─ Response: 201 Created
```

## Performance Considerations

### Database Optimization
- **Indexes**: On frequently queried columns (device_id, tenant_id, rollout_id)
- **TimescaleDB**: Automatic compression and retention policies for time-series data
- **Connection Pooling**: EF Core with connection pooling enabled

### Caching Strategy
- **Cache-Aside Pattern**: Check cache → miss → fetch from DB → update cache
- **TTL-based Expiration**: Different TTLs based on data volatility
- **Cache Warming**: Pre-populate cache for frequently accessed data

### Async Processing
- **Background Jobs**: Long-running tasks via NATS
- **Message Queues**: Decoupled processing for scalability
- **Retry Policies**: Polly for transient failures

## Scalability

### Horizontal Scaling
- **Stateless Services**: All services are stateless and can scale horizontally
- **Database Read Replicas**: For read-heavy workloads
- **Message Broker Clustering**: NATS cluster for high availability

### Vertical Scaling
- **Resource Limits**: Kubernetes resource requests/limits
- **Auto-scaling**: HPA based on CPU/memory/custom metrics

---

**Next Steps**:
- [Local Development Guide](../development/local-development.md)
- [Rollout Feature Documentation](../features/rollouts.md)
- [Domain Model](domain-model.md)
