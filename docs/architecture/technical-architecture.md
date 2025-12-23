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

```
┌─────────────────────────────────────────────────────────────┐
│                        Web UI (React)                        │
│              http://localhost:5173 (dev)                     │
└─────────────────────┬───────────────────────────────────────┘
                      │ HTTPS/REST
                      ▼
┌─────────────────────────────────────────────────────────────┐
│                    Backend Services                          │
│  ┌─────────────────┐  ┌──────────────────┐  ┌─────────────┐│
│  │ DeviceManager   │  │BundleOrchestrator│  │  Telemetry  ││
│  │   (Port 5000)   │  │   (Port 5002)    │  │  Processor  ││
│  │                 │  │                  │  │             ││
│  │ • Devices       │  │ • Bundles        │  │ • Metrics   ││
│  │ • Groups        │  │ • Versions       │  │ • Heartbeat ││
│  │ • Tags          │  │ • Rollouts       │  │             ││
│  └─────────────────┘  └──────────────────┘  └─────────────┘│
└─────────────────────┬─────────────────────────────────┬─────┘
                      │                                 │
                      ▼                                 ▼
┌─────────────────────────────────────┐  ┌──────────────────────┐
│      Infrastructure Layer            │  │   Message Broker     │
│                                      │  │                      │
│  • PostgreSQL (+ TimescaleDB)       │  │  • NATS + JetStream  │
│  • Valkey (Redis-compatible)        │  │  • Event Streaming   │
│  • Azure Blob Storage (Azurite)     │  │  • Pub/Sub           │
└─────────────────────────────────────┘  └──────────────────────┘
                      ▲
                      │ HTTP/REST + Message Queue
                      │
┌─────────────────────┴───────────────────────────────────────┐
│                    Edge Agent (on device)                    │
│  • Heartbeat sender    • Container reconciliation           │
│  • Status reporter     • Docker/Podman interface            │
└─────────────────────────────────────────────────────────────┘
```

## Architectural Patterns

### 1. Hexagonal Architecture (Ports & Adapters)

Each microservice follows the **Hexagonal Architecture** pattern:

```
┌──────────────────────────────────────────────────────────┐
│                    SignalBeam Service                     │
│                                                           │
│  ┌────────────────────────────────────────────────────┐  │
│  │              Host Layer (Web API)                  │  │
│  │  • Minimal API Endpoints                           │  │
│  │  • Middleware Pipeline                             │  │
│  │  • Dependency Injection                            │  │
│  └────────────────┬───────────────────────────────────┘  │
│                   │                                       │
│  ┌────────────────▼───────────────────────────────────┐  │
│  │          Application Layer (CQRS)                  │  │
│  │  • Command Handlers                                │  │
│  │  • Query Handlers                                  │  │
│  │  • Application Services                            │  │
│  │  • DTOs & Validators                               │  │
│  └────────────────┬───────────────────────────────────┘  │
│                   │                                       │
│  ┌────────────────▼───────────────────────────────────┐  │
│  │          Domain Layer (Business Logic)             │  │
│  │  • Entities & Aggregates                           │  │
│  │  • Value Objects                                   │  │
│  │  • Domain Events                                   │  │
│  │  • Business Rules                                  │  │
│  └────────────────┬───────────────────────────────────┘  │
│                   │                                       │
│  ┌────────────────▼───────────────────────────────────┐  │
│  │       Infrastructure Layer (Adapters)              │  │
│  │  • EF Core DbContext                               │  │
│  │  • Repository Implementations                      │  │
│  │  • External Service Clients                        │  │
│  │  • Message Publishers                              │  │
│  └────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────┘
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
```
signalbeam.
├── devices.
│   ├── registered              # Device registration events
│   ├── heartbeat.<deviceId>    # Heartbeat stream
│   ├── status.<deviceId>       # Status changes
│   └── events.<eventType>      # General device events
├── bundles.
│   ├── assigned                # Bundle assignment events
│   ├── rollouts.<rolloutId>    # Rollout progress updates
│   └── artifacts.uploaded      # Artifact notifications
└── telemetry.
    ├── metrics.<deviceId>      # Device metrics stream
    └── logs.<deviceId>         # Log streaming (future)
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

```
┌─────────────────────────────────────────────────────────┐
│             .NET Aspire AppHost                          │
│  Orchestrates all services and infrastructure           │
└─────────────┬───────────────────────────────────────────┘
              │
              ├─→ PostgreSQL (container)
              ├─→ Valkey (container)
              ├─→ NATS (container)
              ├─→ Azurite (container)
              │
              ├─→ DeviceManager (dotnet run)
              ├─→ BundleOrchestrator (dotnet run)
              └─→ TelemetryProcessor (dotnet run)

Aspire Dashboard: http://localhost:15888
```

### Production Deployment (Kubernetes)

```
┌─────────────────────────────────────────────────────────┐
│                  Kubernetes Cluster (AKS)                │
│                                                           │
│  ┌──────────────────────────────────────────────────┐   │
│  │            Ingress (NGINX/App Gateway)            │   │
│  │  • TLS termination                                │   │
│  │  • Rate limiting                                  │   │
│  │  • Load balancing                                 │   │
│  └─────────────────┬────────────────────────────────┘   │
│                    │                                     │
│  ┌─────────────────▼────────────────────────────────┐   │
│  │              Service Mesh (Cilium)                │   │
│  │  • mTLS                                           │   │
│  │  • Traffic management                             │   │
│  │  • Observability                                  │   │
│  └─────────────────┬────────────────────────────────┘   │
│                    │                                     │
│  ┌─────────────────▼────────────────────────────────┐   │
│  │            Microservices (Pods)                   │   │
│  │                                                    │   │
│  │  DeviceManager      BundleOrchestrator            │   │
│  │  (3 replicas)       (3 replicas)                  │   │
│  │                                                    │   │
│  │  TelemetryProcessor                               │   │
│  │  (3 replicas)                                     │   │
│  └───────────────────────────────────────────────────┘   │
│                                                           │
│  ┌───────────────────────────────────────────────────┐   │
│  │        External Services (Managed)                 │   │
│  │  • Azure Database for PostgreSQL                  │   │
│  │  • Azure Cache for Redis (Valkey)                 │   │
│  │  • Azure Blob Storage                             │   │
│  │  • NATS (self-hosted cluster)                     │   │
│  └───────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
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
