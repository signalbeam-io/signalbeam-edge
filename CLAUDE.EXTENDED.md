# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SignalBeam Edge is a fleet management platform for edge devices (e.g., Raspberry Pis, mini-PCs) that enables onboarding, monitoring, and updating containerized applications across distributed devices from a centralized dashboard.

**Core value proposition:** Manage N edge devices running Docker containers without SSH'ing into individual boxes.

## Repository Structure

```
signalbeam-edge/
├── src/
│   ├── SignalBeam.sln                           # Main solution file
│   ├── Directory.Build.props                    # Global build settings
│   ├── Directory.Packages.props                 # Central package management
│   │
│   ├── SignalBeam.AppHost/                      # .NET Aspire orchestrator
│   │   └── Program.cs                           # Local dev service orchestration
│   │
│   ├── SignalBeam.ServiceDefaults/              # Aspire shared configuration
│   │   └── Extensions.cs                        # OpenTelemetry, health checks
│   │
│   ├── SignalBeam.Domain/                       # Shared domain layer
│   │   ├── Abstractions/                        # Base classes (Entity, AggregateRoot, ValueObject)
│   │   ├── Entities/                            # Domain entities (Device, AppBundle, etc.)
│   │   ├── Events/                              # Domain events
│   │   └── ValueObjects/                        # Value objects (DeviceId, BundleVersion, etc.)
│   │
│   ├── SignalBeam.Shared.Infrastructure/        # Shared infrastructure concerns
│   │   ├── Observability/                       # OpenTelemetry, Serilog
│   │   ├── Resilience/                          # Polly policies
│   │   ├── Authentication/                      # JWT/OIDC, API key validation
│   │   ├── Results/                             # Result pattern implementation
│   │   ├── Messaging/                           # Message broker abstractions
│   │   └── Time/                                # Time abstractions for testing
│   │
│   ├── DeviceManager/                           # Microservice: Device registration & state
│   │   ├── DeviceManager.Application/           # Use cases, CQRS handlers
│   │   ├── DeviceManager.Infrastructure/        # EF Core, repositories, external services
│   │   ├── DeviceManager.Host/                  # Web API host, endpoints, DI configuration
│   │   └── charts/                              # Helm chart for Kubernetes deployment
│   │
│   ├── BundleOrchestrator/                      # Microservice: Bundle management & rollouts
│   │   ├── BundleOrchestrator.Application/
│   │   ├── BundleOrchestrator.Infrastructure/
│   │   ├── BundleOrchestrator.Host/
│   │   └── charts/
│   │
│   ├── TelemetryProcessor/                      # Microservice: Metrics & heartbeat processing
│   │   ├── TelemetryProcessor.Application/
│   │   ├── TelemetryProcessor.Infrastructure/
│   │   ├── TelemetryProcessor.Host/
│   │   └── charts/
│   │
│   └── EdgeAgent/                               # Edge device agent (console app)
│       ├── EdgeAgent.Application/
│       ├── EdgeAgent.Infrastructure/
│       └── EdgeAgent.Host/
│
├── web/                                         # React + TypeScript frontend
│   ├── src/
│   │   ├── components/
│   │   ├── features/
│   │   ├── hooks/
│   │   └── api/
│   ├── charts/                                  # Helm chart for frontend
│   ├── package.json
│   └── tsconfig.json
│
├── deploy/                                      # Kubernetes & Helm deployments
│   ├── charts/                                  # Umbrella Helm charts
│   │   ├── signalbeam-platform/                # Main platform chart
│   │   └── signalbeam-infrastructure/          # Infrastructure dependencies (postgres, kafka, etc.)
│   └── kustomize/                               # Kustomize overlays (optional)
│       ├── base/
│       ├── dev/
│       ├── staging/
│       └── prod/
│
├── infra/                                       # Infrastructure as Code
│   ├── terraform/
│   │   ├── modules/                             # Reusable Terraform modules
│   │   │   ├── k8s-cluster/                    # Kubernetes cluster (AKS, EKS, GKE, or self-managed)
│   │   │   ├── networking/                      # VPC, subnets, security groups
│   │   │   ├── database/                        # PostgreSQL (RDS, CloudSQL, or self-hosted)
│   │   │   ├── storage/                         # S3/MinIO for object storage
│   │   │   ├── kafka/                           # Kafka cluster (optional)
│   │   │   ├── observability/                   # Prometheus, Grafana, Loki, Tempo
│   │   │   └── dns/                             # DNS and ingress configuration
│   │   └── live/                                # Live infrastructure configurations
│   │       ├── dev/
│   │       ├── staging/
│   │       └── prod/
│   │
│   └── terragrunt/
│       ├── terragrunt.hcl                       # Root configuration
│       ├── dev/
│       │   ├── terragrunt.hcl                  # Environment-specific config
│       │   ├── k8s-cluster/
│       │   ├── database/
│       │   └── storage/
│       ├── staging/
│       └── prod/
│
├── tests/
│   ├── SignalBeam.Domain.Tests/
│   ├── DeviceManager.Tests.Integration/
│   ├── EdgeAgent.Tests.Integration/
│   └── E2E.Tests/                               # End-to-end tests in Kubernetes
│
├── scripts/                                     # Build and deployment scripts
│   ├── build-images.sh                          # Build Docker images for all services
│   ├── deploy-dev.sh                            # Deploy to dev environment
│   └── run-local.sh                             # Local development with Tilt
│
├── docs/
│   ├── architecture/
│   ├── api/
│   └── runbooks/
│
├── Tiltfile                                     # Tilt configuration for local K8s development
└── docker-compose.yml                           # Local development without K8s
```

## Technical Stack

### Backend (.NET 10)
- **Framework:** .NET 10.0, C# 14
- **Code Quality:** Nullable reference types, treat warnings as errors, Roslynator + SonarAnalyzer
- **Architecture:** Hexagonal architecture with clear domain/application/infrastructure separation

### Core Packages

**Messaging & CQRS:**
- `WolverineFx` (latest) - Message handling, CQRS pattern, background processing
- `WolverineFx.Http` (latest) - HTTP endpoint generation
- Custom NATS integration with Wolverine (or direct NATS.Client usage)

**Data Access:**
- `Microsoft.EntityFrameworkCore` (10.0.0) - ORM
- `Npgsql.EntityFrameworkCore.PostgreSQL` (latest) - PostgreSQL provider
- `Dapper` (latest) - Lightweight queries

**Cloud-Native Integrations:**
- `Azure.Storage.Blobs` (latest) - Azure Blob Storage for bundle artifacts
- `NATS.Client` (latest) - Messaging and event streaming with JetStream
- `StackExchange.Redis` (latest) - Distributed caching (connects to Valkey)
- `Azure.Identity` (latest) - Azure AD authentication with Managed Identity

**Authentication & Authorization:**
- `Microsoft.AspNetCore.Authentication.JwtBearer` (10.0.0) - JWT authentication
- `Microsoft.Identity.Web` (latest) - Microsoft Entra ID (Azure AD) integration
- `Microsoft.AspNetCore.Authentication.OpenIdConnect` (10.0.0) - OIDC integration (Entra ID, Zitadel)
- `Azure.Identity` (latest) - Managed Identity for Azure resources

**Observability:**
- `OpenTelemetry` (latest) - Distributed tracing and metrics
- `OpenTelemetry.Instrumentation.AspNetCore`
- `OpenTelemetry.Instrumentation.EntityFrameworkCore`
- `OpenTelemetry.Instrumentation.Http`
- `OpenTelemetry.Exporter.Prometheus.AspNetCore` - Prometheus metrics exporter
- `OpenTelemetry.Exporter.OpenTelemetryProtocol` - OTLP exporter (for Tempo, Jaeger)
- `Serilog.AspNetCore` (latest) - Structured logging
- `Serilog.Sinks.Console`, `Serilog.Sinks.File`
- `Serilog.Sinks.Grafana.Loki` - Loki log aggregation
- `Serilog.Enrichers.Environment`, `Serilog.Enrichers.Thread`

**Resilience:**
- `Polly` (latest) - Retry, circuit breaker, timeout policies
- `Polly.Extensions`

**Validation & Mapping:**
- `FluentValidation` (latest) - Request validation
- `Riok.Mapperly` (latest) - Source generators for mapping

**Web API:**
- `Microsoft.AspNetCore.OpenApi` (10.0.0)
- `Scalar.AspNetCore` (latest) - Modern API documentation with beautiful UI
- `AspNetCoreRateLimit` (latest) - Rate limiting

**Health Checks:**
- `AspNetCore.HealthChecks.Npgsql` - PostgreSQL/TimescaleDB health check
- Custom NATS health check implementation
- `AspNetCore.HealthChecks.Redis` - Valkey health check

**Local Development:**
- `Aspire.Hosting` (latest) - .NET Aspire for local orchestration
- `Aspire.Hosting.PostgreSQL` - PostgreSQL integration
- `Aspire.Hosting.Redis` - Valkey/Redis integration
- `Aspire.Hosting.NATS` - NATS integration (custom)

### Testing Stack
- `xUnit` (latest) - Test framework
- `FluentAssertions` (latest) - Assertion library
- `NSubstitute` (latest) - Mocking
- `Testcontainers` (latest) - Integration tests with PostgreSQL/TimescaleDB, NATS, MinIO, Valkey
- `WireMock.Net` (latest) - HTTP mocking
- `NetArchTest.Rules` (latest) - Architecture tests

### Frontend (React + TypeScript)
- React 18+
- TypeScript
- Component library: shadcn/ui (Tailwind CSS-based)
- State management: TanStack Query (React Query) for server state, Zustand for client state
- Build tool: Vite
- Routing: React Router v6

### Infrastructure (Cloud-Native)

**Container Orchestration:**
- **Azure Kubernetes Service (AKS)** - Managed Kubernetes on Azure
- **Helm** - Package manager for Kubernetes
- **Kustomize** - Configuration management (optional)

**Infrastructure as Code:**
- **Terraform** - Infrastructure provisioning
- **Terragrunt** - DRY Terraform configuration management

**Observability Stack:**
- **Azure Monitor** - Native Azure monitoring (optional integration)
  - Container Insights for AKS monitoring
  - Log Analytics workspace for centralized logs
- **Prometheus** - Metrics collection (can forward to Azure Monitor)
- **Grafana** - Metrics and logs visualization (Azure Managed Grafana available)
- **Loki** or **OpenObserve** - Log aggregation (OpenObserve: 140x lower storage cost, faster queries)
- **Tempo** - Distributed tracing backend
- **AlertManager** - Alert routing (can integrate with Azure Monitor alerts)

**Data Storage:**
- **Azure Database for PostgreSQL Flexible Server with TimescaleDB** - Managed PostgreSQL with time-series extension
  - Time-series data for device heartbeats, metrics, telemetry
  - 10-100x faster queries on time-series data
  - Automatic data retention policies and compression
  - Continuous aggregates for real-time dashboards
  - Built-in high availability and automated backups
- **Azure Blob Storage** - Object storage for bundle artifacts (S3-compatible via MinIO gateway if needed)
- **Azure Container Instances for Valkey** - Containerized Valkey on Azure
  - Open-source distributed cache (Linux Foundation Redis fork)
  - Drop-in Redis replacement with better licensing (BSD-3)
  - Alternative: Azure Cache for Redis (commercial offering)

**Message Broker:**
- **NATS with JetStream** - Lightweight, cloud-native messaging system
  - Pub/Sub for event distribution
  - JetStream for persistent messaging and stream processing
  - Key-Value store for distributed configuration
  - Object store for large payloads
  - Request-Reply for synchronous communication

**Service Mesh (optional, for production):**
- **Cilium** - eBPF-based CNI and service mesh (recommended)
  - Kernel-level networking for better performance
  - Lower overhead than Istio/Linkerd
  - Unified CNI + Service Mesh + Security
  - Hubble for observability
- **Linkerd** - Lightweight alternative (non-eBPF)

**Ingress:**
- **NGINX Ingress Controller** - Kubernetes ingress (or Azure Application Gateway Ingress Controller)
- **Cert-Manager** - Automatic TLS certificate management (Let's Encrypt)
- **Azure Front Door** - Global CDN and WAF (optional, for production)

**CI/CD:**
- **GitHub Actions** - Build and deployment pipelines (integrated with Azure)
- **Azure Container Registry (ACR)** - Private container registry with geo-replication
- **ArgoCD** - GitOps continuous deployment to AKS
- **Kustomize** - Configuration management for ArgoCD

## Architectural Patterns

### Hexagonal Architecture (Ports & Adapters)
Each microservice follows clean architecture:

1. **Domain Layer** (`SignalBeam.Domain`)
   - Pure business logic, no framework dependencies
   - Entities, value objects, domain events
   - Repository interfaces (ports)

2. **Application Layer** (`*.Application`)
   - Use cases and business workflows
   - CQRS command/query handlers (using Wolverine)
   - Application services
   - Validation rules

3. **Infrastructure Layer** (`*.Infrastructure`)
   - Adapters for external systems
   - EF Core repositories
   - Cloud storage integrations (S3/MinIO)
   - NATS client integration (pub/sub, JetStream)
   - External API clients
   - Redis cache implementation

4. **Host Layer** (`*.Host`)
   - ASP.NET Core web API
   - Minimal API endpoints
   - Dependency injection configuration
   - Middleware pipeline

### CQRS with Wolverine
- Commands: State-changing operations (RegisterDevice, AssignBundle)
- Queries: Read-only operations (GetDeviceStatus, ListBundles)
- Wolverine handles message routing, middleware, and background processing
- CloudEvents 1.0 standard for event schema

### Event-Driven Design
- Domain events published for state changes
- Event handlers process side effects asynchronously
- Example: `DeviceRegisteredEvent` → Update search index, send welcome notification

### Repository Pattern
- Generic repository in Domain for data access abstraction
- EF Core implementation in Infrastructure
- Dapper for read-optimized queries

### Result Pattern
- Operations return `Result<T>` instead of throwing exceptions
- Explicit error handling with typed error cases
- Prevents exceptions for business rule violations

## System Architecture

### 1. SignalBeam Edge Agent (.NET Console App)
- **Deployment:** Systemd service on Linux (ARM/x86)
- **Responsibilities:**
  - Device registration with cloud using tenantId, deviceId, registration token
  - Periodic heartbeats with metrics (uptime, CPU, memory, disk usage)
  - Reconciliation loop:
    - Fetches desired bundle definition from cloud API
    - Compares with currently running containers
    - Pulls/starts/stops containers to match desired state
  - Reports current bundle version and container statuses
- **Container Management:** Docker CLI/SDK or k3s API
- **Configuration:** appsettings.json with cloud endpoint, polling intervals

### 2. DeviceManager Service (Microservice)
- **Responsibilities:**
  - Device registration and authentication
  - Device CRUD operations
  - Device grouping and tagging
  - Heartbeat ingestion and device status tracking
  - Device event log
- **Database Tables:** Device, DeviceTag, DeviceGroup, DeviceHeartbeat, DeviceEvent
- **Auth:** API key validation (MVP), JWT/OIDC (production - Keycloak, Auth0, etc.)
- **Deployment:** Kubernetes Deployment with HPA (Horizontal Pod Autoscaler)

### 3. BundleOrchestrator Service (Microservice)
- **Responsibilities:**
  - App Bundle CRUD and versioning
  - Bundle assignment to devices/groups (desired state)
  - Rollout orchestration and status tracking
  - Bundle artifact management (container image references)
- **Database Tables:** AppBundle, AppBundleVersion, DeviceDesiredState, RolloutStatus
- **Storage:** S3-compatible object storage (MinIO/AWS S3/GCS) for bundle metadata/artifacts
- **Deployment:** Kubernetes Deployment with persistent volume for caching

### 4. TelemetryProcessor Service (Microservice)
- **Responsibilities:**
  - Process device heartbeats and metrics
  - Aggregate metrics for dashboard
  - Store reported state (current bundle, container status)
  - Anomaly detection (future)
- **Database Tables:** DeviceReportedState, DeviceMetrics
- **Processing:** Wolverine message handlers for async processing
- **Message Broker:** Consumes from NATS JetStream subjects
- **Deployment:** Kubernetes Deployment with multiple replicas for high availability

### 5. SignalBeam Web UI (React SPA)
- **Features:**
  - Fleet overview dashboard (device table with status, metrics, tags)
  - Device detail pages (health metrics, container status, activity log)
  - App Bundle management (CRUD, versioning)
  - Bundle assignment to devices/groups
  - Rollout progress tracking
- **Deployment:** Kubernetes Deployment with NGINX ingress
- **API Gateway:** Calls backend microservices via Kubernetes services
- **Static Assets:** Served via CDN or Kubernetes ingress with caching

## Key Concepts

### App Bundles
Named, versioned definitions of container sets to run on devices. Example:
```json
{
  "bundleId": "warehouse-monitor",
  "version": "1.2.0",
  "containers": [
    { "name": "temp-sensor", "image": "ghcr.io/org/temp-sensor:1.2.0" },
    { "name": "relay-controller", "image": "ghcr.io/org/relay-controller:2.0.1" }
  ]
}
```

### Desired State Model
- Cloud stores desired bundle assignment per device
- Agent polls for its desired state and reconciles with actual state
- Rollout tracking shows: Pending → Updating → Succeeded/Failed

### Device Grouping
- Devices can be tagged (`lab`, `prod`, `rpi`, `x86`)
- Logical groups serve as rollout targets
- Bundles assigned to groups propagate to all member devices

## MVP Scope (v0.1)

**In scope:**
- Device registration and heartbeat monitoring
- Basic health metrics (CPU, memory, disk)
- Device grouping and tagging
- App bundle definition and versioning
- Bundle assignment to devices/groups
- Agent reconciliation loop for container management
- Simple rollout status tracking
- Basic event log per device

**Explicitly out of scope for MVP:**
- AI/ML model deployment
- Advanced security (PKI, mTLS, per-device certs)
- Multi-tenant billing or RBAC UI
- Complex rollout strategies (blue/green, canary)
- Air-gapped/on-prem only deployments
- Deep log aggregation or time-series metrics UI

## Database Entities

Core entities to implement:
- `Device` - edge device records
- `DeviceTag` - tagging system
- `DeviceGroup` - logical groupings
- `AppBundle` - bundle definitions
- `AppBundleVersion` - versioned bundle specs
- `DeviceDesiredState` - assigned bundles per device
- `DeviceReportedState` - current state from agents
- `DeviceEvent` / `ActivityLog` - event history

## Development Setup

### Prerequisites
- **.NET 10 SDK** - Backend development
- **.NET Aspire Workload** - Local development orchestration: `dotnet workload install aspire`
- **Docker Desktop** - Containers and local development
- **Kubernetes** - Local cluster (Docker Desktop, minikube, kind, or k3d)
- **kubectl** - Kubernetes CLI
- **Helm 3** - Kubernetes package manager
- **Tilt** - Local Kubernetes development (optional but recommended)
- **Node.js 20+** - Frontend development
- **Terraform 1.5+** - Infrastructure provisioning
- **Terragrunt 0.50+** - Terraform wrapper for DRY configurations
- **Azure CLI** - Azure cloud management: `az`
- **kubelogin** - Azure AD authentication for AKS: `kubelogin`

### Common Commands

**Backend (.NET):**
```bash
# Restore dependencies (uses central package management)
dotnet restore

# Build entire solution
dotnet build

# Run a specific service (from service Host directory)
cd src/DeviceManager/DeviceManager.Host
dotnet run

# Run all tests
dotnet test

# Run tests with coverage
dotnet test /p:CollectCoverage=true

# Run integration tests (requires Docker)
dotnet test --filter Category=Integration

# Apply database migrations
cd src/DeviceManager/DeviceManager.Infrastructure
dotnet ef database update

# Create new migration
dotnet ef migrations add <MigrationName>

# Format code
dotnet format

# Build Docker image for a service
docker build -f src/DeviceManager/DeviceManager.Host/Dockerfile -t signalbeam/device-manager:dev .
```

**Frontend:**
```bash
# Install dependencies
cd web
npm install

# Run dev server
npm run dev

# Build for production
npm run build

# Run tests
npm test

# Lint
npm run lint

# Build Docker image
docker build -t signalbeam/web-ui:dev .
```

**Kubernetes & Helm:**
```bash
# Verify kubectl is connected to your cluster
kubectl cluster-info
kubectl get nodes

# Install infrastructure dependencies (PostgreSQL, NATS, Redis, etc.)
helm install signalbeam-infra deploy/charts/signalbeam-infrastructure -n signalbeam --create-namespace

# Or install individually:
# PostgreSQL
helm install postgresql bitnami/postgresql -n signalbeam --set auth.database=signalbeam

# NATS with JetStream
helm install nats nats/nats -n signalbeam \
  --set nats.jetstream.enabled=true \
  --set nats.jetstream.fileStorage.size=10Gi \
  --set cluster.enabled=true \
  --set cluster.replicas=3

# Valkey (Redis fork)
helm install valkey bitnami/redis -n signalbeam \
  --set image.repository=valkey/valkey \
  --set image.tag=7.2

# Install the platform (all microservices)
helm install signalbeam deploy/charts/signalbeam-platform -n signalbeam

# Upgrade a release
helm upgrade signalbeam deploy/charts/signalbeam-platform -n signalbeam

# List all releases
helm list -n signalbeam

# Uninstall
helm uninstall signalbeam -n signalbeam

# Install a single service chart (for development)
helm install device-manager src/DeviceManager/charts -n signalbeam

# Check pod status
kubectl get pods -n signalbeam

# View logs
kubectl logs -f deployment/device-manager -n signalbeam

# Port-forward to a service
kubectl port-forward svc/device-manager 8080:80 -n signalbeam

# Execute command in a pod
kubectl exec -it deployment/device-manager -n signalbeam -- /bin/bash

# Apply a manifest
kubectl apply -f deploy/kustomize/dev

# View service details
kubectl describe svc device-manager -n signalbeam
```

**Infrastructure (Terraform + Terragrunt):**
```bash
# Navigate to environment
cd infra/terragrunt/dev

# Initialize all modules in an environment
terragrunt run-all init

# Plan all changes
terragrunt run-all plan

# Apply all infrastructure
terragrunt run-all apply

# Apply a specific module
cd infra/terragrunt/dev/k8s-cluster
terragrunt apply

# Destroy all infrastructure
terragrunt run-all destroy

# Validate configuration
terragrunt validate

# Format Terraform files
terragrunt hclfmt

# Show dependency graph
terragrunt graph-dependencies

# Using plain Terraform (if needed)
cd infra/terraform/live/dev
terraform init
terraform plan
terraform apply
```

**Local Development with Tilt (Recommended):**
```bash
# Start all services in local Kubernetes with hot reload
tilt up

# Open Tilt UI
# Browser will open automatically at http://localhost:10350

# Stop Tilt
tilt down

# View logs for a specific service
tilt logs device-manager
```

**Local Development with .NET Aspire (Recommended):**
```bash
# Install .NET Aspire workload
dotnet workload install aspire

# Run the Aspire AppHost project (starts all services)
cd src/SignalBeam.AppHost
dotnet run

# Aspire dashboard opens automatically at http://localhost:15888
# - View all services and their logs
# - Access metrics and traces
# - Service discovery built-in
```

**Local Development with Docker Compose (Alternative):**
```bash
# Start all services locally
docker-compose up -d

# View logs
docker-compose logs -f

# View logs for specific service
docker-compose logs -f device-manager

# Stop all services
docker-compose down

# Rebuild and start
docker-compose up -d --build

# Remove volumes (clean state)
docker-compose down -v
```

**Database Operations:**
```bash
# Connect to PostgreSQL/TimescaleDB in Kubernetes
kubectl exec -it deployment/postgresql -n signalbeam -- psql -U postgres -d signalbeam

# Enable TimescaleDB extension
kubectl exec -it deployment/postgresql -n signalbeam -- psql -U postgres -d signalbeam -c "CREATE EXTENSION IF NOT EXISTS timescaledb;"

# Port-forward PostgreSQL for local access
kubectl port-forward svc/postgresql 5432:5432 -n signalbeam

# Run migrations in Kubernetes
kubectl exec -it deployment/device-manager -n signalbeam -- dotnet ef database update
```

### Project Structure Conventions

**Each microservice follows this structure:**
```
ServiceName/
├── ServiceName.Application/
│   ├── Commands/              # CQRS command handlers
│   ├── Queries/               # CQRS query handlers
│   ├── Events/                # Event handlers
│   ├── Services/              # Application services
│   ├── Validators/            # FluentValidation validators
│   └── Contracts/             # DTOs, request/response models
│
├── ServiceName.Infrastructure/
│   ├── Persistence/
│   │   ├── Configurations/    # EF Core entity configurations
│   │   ├── Repositories/      # Repository implementations
│   │   └── DbContext.cs
│   ├── ExternalServices/      # Third-party API clients
│   └── DependencyInjection.cs
│
└── ServiceName.Host/
    ├── Program.cs             # Application entry point
    ├── Endpoints/             # Minimal API endpoints
    ├── Middleware/            # Custom middleware
    ├── appsettings.json
    └── appsettings.Development.json
```

### Central Package Management

This project uses `Directory.Packages.props` for centralized NuGet package version management:
- Package versions defined once in `src/Directory.Packages.props`
- Projects reference packages without version numbers: `<PackageReference Include="Serilog.AspNetCore" />`
- Update package versions in one place

### Code Quality Standards

**Enforced via Directory.Build.props:**
- Nullable reference types enabled
- Treat warnings as errors (level 9999)
- Implicit usings enabled
- .NET code analyzers active (latest-recommended)
- Roslynator.Analyzers for additional rules
- SonarAnalyzer.CSharp for code quality

**Architecture Testing:**
- Use `NetArchTest.Rules` to enforce architectural boundaries
- Example tests:
  - Domain layer has no dependencies on Infrastructure
  - Application layer doesn't reference Host layer
  - Controllers/Endpoints only depend on Application layer

### Testing Strategy

**Unit Tests:**
- Test domain entities and value objects
- Test application layer handlers and services
- Use NSubstitute for mocking dependencies
- Fast, isolated, no external dependencies

**Integration Tests:**
- Use Testcontainers to spin up real PostgreSQL and other services
- Test full request pipeline including database
- Test API endpoints end-to-end
- Clean database state between tests

**Architecture Tests:**
- Verify layering rules (Domain → Application → Infrastructure → Host)
- Ensure no circular dependencies
- Check naming conventions

### Development Workflow

1. **Create feature branch** from `main`
2. **Domain modeling:** Start with Domain entities and value objects
3. **Application logic:** Implement command/query handlers
4. **Infrastructure:** Add repository implementations and external integrations
5. **Endpoints:** Define minimal API endpoints in Host layer
6. **Tests:** Write unit and integration tests
7. **Documentation:** Update API documentation (OpenAPI/Swagger auto-generated)
8. **PR review:** Submit PR with tests passing and architecture rules validated

### Environment Configuration

**appsettings.json structure:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=signalbeam;Username=postgres;Password=..."
  },
  "AzureBlobStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=signalbeam;AccountKey=...;EndpointSuffix=core.windows.net",
    "ContainerName": "signalbeam-bundles",
    "UseManagedIdentity": true  // Use Azure Managed Identity in production
  },
  "NATS": {
    "Url": "nats://localhost:4222",
    "JetStream": {
      "Enabled": true,
      "StorageType": "File"
    },
    "Subjects": {
      "DeviceHeartbeat": "signalbeam.devices.heartbeat",
      "DeviceEvents": "signalbeam.devices.events",
      "BundleAssignments": "signalbeam.bundles.assignments"
    }
  },
  "Valkey": {
    "ConnectionString": "localhost:6379,abortConnect=false"
  },
  "TimescaleDB": {
    "EnableCompression": true,
    "RetentionDays": 90,
    "ChunkTimeInterval": "1 day"
  },
  "Authentication": {
    "AzureAd": {
      "Instance": "https://login.microsoftonline.com/",
      "TenantId": "your-tenant-id",
      "ClientId": "your-client-id",
      "Audience": "api://signalbeam-api"
    },
    "Jwt": {
      "Authority": "https://login.microsoftonline.com/your-tenant-id/v2.0",  // Microsoft Entra ID (or Zitadel)
      "Audience": "api://signalbeam-api",
      "RequireHttpsMetadata": true
    },
    "ApiKey": {
      "HeaderName": "X-API-Key",
      "Enabled": true
    }
  },
  "EdgeAgent": {
    "HeartbeatIntervalSeconds": 30,
    "ReconciliationIntervalSeconds": 60,
    "MaxRetries": 3
  },
  "Observability": {
    "OpenTelemetry": {
      "ServiceName": "device-manager",
      "ServiceVersion": "1.0.0",
      "OtlpExporterEndpoint": "http://localhost:4317",  // Tempo/Jaeger
      "PrometheusEndpoint": "/metrics"
    },
    "Serilog": {
      "MinimumLevel": "Information",
      "WriteTo": [
        {
          "Name": "Console",
          "Args": {
            "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
          }
        },
        {
          "Name": "GrafanaLoki",
          "Args": {
            "uri": "http://localhost:3100",  // Or OpenObserve: http://localhost:5080
            "labels": [
              {
                "key": "app",
                "value": "device-manager"
              }
            ]
          }
        }
      ]
    }
  },
  "HealthChecks": {
    "Enabled": true,
    "UIPath": "/healthchecks-ui"
  }
}
```

**Kubernetes ConfigMap & Secrets:**
```yaml
# ConfigMap for non-sensitive configuration
apiVersion: v1
kind: ConfigMap
metadata:
  name: device-manager-config
  namespace: signalbeam
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  EdgeAgent__HeartbeatIntervalSeconds: "30"
  EdgeAgent__ReconciliationIntervalSeconds: "60"
  NATS__Url: "nats://nats.signalbeam.svc.cluster.local:4222"
  NATS__JetStream__Enabled: "true"
  OpenTelemetry__ServiceName: "device-manager"

---
# Secret for sensitive data
apiVersion: v1
kind: Secret
metadata:
  name: device-manager-secrets
  namespace: signalbeam
type: Opaque
stringData:
  ConnectionStrings__DefaultConnection: "Host=signalbeam-db.postgres.database.azure.com;Database=signalbeam;Username=dbadmin;Password=...;SslMode=Require"
  AzureBlobStorage__ConnectionString: "DefaultEndpointsProtocol=https;AccountName=signalbeam;AccountKey=...;EndpointSuffix=core.windows.net"
  NATS__Username: "signalbeam"
  NATS__Password: "..."
  AzureAd__ClientSecret: "..."
```

**Use User Secrets for local development:**
```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=signalbeam;Username=postgres;Password=postgres"
dotnet user-secrets set "AzureBlobStorage:ConnectionString" "UseDevelopmentStorage=true"  # For Azurite local emulator
dotnet user-secrets set "AzureAd:ClientSecret" "your-dev-client-secret"
dotnet user-secrets set "NATS:Password" "natspassword"
```

**Environment Variables in Dockerfile:**
```dockerfile
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
```

## Development Principles

### Core Principles
1. **Target platform:** Linux edge devices (Raspberry Pi, mini-PCs) running containers
2. **Container runtime:** Support Docker first, k3s as option
3. **Agent-cloud communication:** Polling model (agent pulls desired state)
4. **Rollouts:** Immediate "update all targets" for MVP (no staged rollouts yet)
5. **Success metric:** Eliminate manual SSH for fleet updates

### Architecture & Design
6. **Hexagonal architecture:** Strict layer separation (Domain → Application → Infrastructure → Host)
7. **Error handling:** Use Result pattern, avoid exceptions for business logic
8. **Cloud-native first:** Design for Kubernetes, stateless services, 12-factor app principles
9. **API design:** RESTful APIs with OpenAPI documentation, consider gRPC for inter-service communication

### Observability & Monitoring
10. **Distributed tracing:** OpenTelemetry with OTLP exporter to Tempo/Jaeger
11. **Metrics:** Prometheus metrics exposed on `/metrics` endpoint
12. **Logging:** Structured logging with Serilog, shipped to Grafana Loki
13. **Health checks:** Kubernetes liveness and readiness probes on all services
14. **Dashboards:** Grafana dashboards for service metrics, SLOs, and business KPIs

### Resilience & Reliability
15. **Retry policies:** Polly for transient failures (network, database)
16. **Circuit breaker:** Protect against cascading failures
17. **Timeouts:** All external calls have explicit timeouts
18. **Graceful degradation:** Services continue operating with reduced functionality if dependencies fail
19. **Database migrations:** Use EF Core migrations, applied via init containers in Kubernetes

### Infrastructure & Deployment
20. **Infrastructure as Code:** All infrastructure defined in Terraform + Terragrunt
21. **Immutable deployments:** Container images tagged with git SHA, never update in place
22. **GitOps:** ArgoCD for declarative deployments, app manifests in Git
23. **Environments:** Dev, Staging, Production with identical infrastructure (scaled differently)
24. **Secrets management:** Kubernetes Secrets, consider HashiCorp Vault or External Secrets Operator for production

### Testing Strategy
25. **Test pyramid:** Many unit tests, fewer integration tests, minimal E2E tests
26. **Contract testing:** Consumer-driven contracts between services
27. **Testcontainers:** Integration tests with real dependencies (PostgreSQL, NATS)
28. **Load testing:** k6 or Locust for performance validation
29. **NATS subjects:** Use hierarchical naming: `signalbeam.<domain>.<action>` (e.g., `signalbeam.devices.heartbeat`)
30. **Message durability:** Use NATS JetStream for critical events, core NATS for ephemeral messages

## Azure-Specific Architecture

### Why Azure for SignalBeam Edge?
- **Global Reach:** 60+ regions worldwide for edge device connectivity
- **IoT Native:** Azure has deep IoT expertise and services
- **Hybrid:** Supports edge devices with Azure IoT Edge, Azure Arc
- **Enterprise Ready:** Strong compliance, security, and governance
- **Cost Effective:** Reserved instances, spot VMs, dev/test pricing

### Azure Service Mapping

| Component | Azure Service | Why |
|-----------|--------------|-----|
| **Kubernetes** | Azure Kubernetes Service (AKS) | Managed K8s with auto-upgrades, scaling |
| **Database** | Azure Database for PostgreSQL Flexible Server | Managed PostgreSQL with TimescaleDB support |
| **Cache** | Valkey on AKS or Azure Cache for Redis | Open-source or managed option |
| **Storage** | Azure Blob Storage | Scalable object storage, zone-redundant |
| **Container Registry** | Azure Container Registry (ACR) | Geo-replication, security scanning |
| **Identity** | Microsoft Entra ID (Azure AD) | Enterprise SSO, MFA, Conditional Access |
| **Networking** | Azure Virtual Network, Private Link | Network isolation, private endpoints |
| **CDN/WAF** | Azure Front Door | Global CDN, DDoS protection, WAF |
| **Monitoring** | Azure Monitor + Prometheus/Grafana | Unified or hybrid observability |
| **Key Management** | Azure Key Vault | Secrets, keys, certificates management |

### Azure Kubernetes Service (AKS) Configuration

**Production-Ready AKS Cluster Features:**
- **Node Pools:** System pool (3 nodes) + User pool (auto-scale 2-10 nodes)
- **Availability Zones:** Multi-zone deployment for HA
- **Network Plugin:** Azure CNI (or Cilium CNI for eBPF)
- **Network Policy:** Cilium or Azure Network Policy
- **Managed Identity:** Azure AD Workload Identity for pods
- **Container Insights:** Built-in monitoring
- **Automatic Upgrades:** Controlled cluster upgrades
- **Azure Policy:** Enforce security and compliance

**Terraform AKS Module:**
```hcl
module "aks" {
  source = "../../modules/aks-cluster"

  cluster_name        = "signalbeam-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.location

  # Node pools
  system_node_pool = {
    vm_size             = "Standard_D4s_v5"
    node_count          = 3
    availability_zones  = [1, 2, 3]
    enable_auto_scaling = false
  }

  user_node_pools = {
    workload = {
      vm_size             = "Standard_D8s_v5"
      min_count           = 2
      max_count           = 10
      availability_zones  = [1, 2, 3]
      enable_auto_scaling = true
      node_labels = {
        workload = "signalbeam"
      }
    }
  }

  # Networking
  network_plugin      = "azure"  # or "none" for Cilium
  network_policy      = "cilium"
  service_cidr        = "10.0.0.0/16"
  dns_service_ip      = "10.0.0.10"
  pod_cidr            = "10.244.0.0/16"

  # Azure AD integration
  azure_ad_rbac_enabled = true

  # Monitoring
  oms_agent_enabled          = true
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id

  # Managed Identity
  identity_type = "SystemAssigned"

  # Add-ons
  azure_policy_enabled     = true
  http_application_routing = false  # Use ingress controller instead
}
```

### Azure Database for PostgreSQL with TimescaleDB

**Flexible Server Configuration:**
```hcl
resource "azurerm_postgresql_flexible_server" "main" {
  name                = "signalbeam-db-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.location

  sku_name   = "GP_Standard_D4s_v3"  # 4 vCores, 16GB RAM
  storage_mb = 131072  # 128 GB

  version                      = "16"
  backup_retention_days        = 35
  geo_redundant_backup_enabled = true

  # High Availability
  high_availability {
    mode                      = "ZoneRedundant"
    standby_availability_zone = 2
  }

  # Network
  delegated_subnet_id = azurerm_subnet.db.id
  private_dns_zone_id = azurerm_private_dns_zone.postgres.id

  # Authentication
  authentication {
    active_directory_auth_enabled = true
    password_auth_enabled          = true
  }

  # Maintenance window
  maintenance_window {
    day_of_week  = 0  # Sunday
    start_hour   = 2
    start_minute = 0
  }
}

# Enable TimescaleDB extension
resource "azurerm_postgresql_flexible_server_configuration" "timescaledb" {
  name      = "shared_preload_libraries"
  server_id = azurerm_postgresql_flexible_server.main.id
  value     = "timescaledb"
}
```

**Enable TimescaleDB after deployment:**
```sql
-- Connect as admin
CREATE EXTENSION IF NOT EXISTS timescaledb;

-- Verify installation
SELECT extname, extversion FROM pg_extension WHERE extname = 'timescaledb';
```

### Azure Blob Storage Integration

**Storage Account with Private Endpoint:**
```hcl
resource "azurerm_storage_account" "bundles" {
  name                     = "signalbeam${var.environment}"
  resource_group_name      = azurerm_resource_group.main.name
  location                 = var.location
  account_tier             = "Standard"
  account_replication_type = "ZRS"  # Zone-redundant

  # Security
  allow_nested_items_to_be_public = false
  enable_https_traffic_only       = true
  min_tls_version                 = "TLS1_2"

  # Network rules
  network_rules {
    default_action             = "Deny"
    virtual_network_subnet_ids = [azurerm_subnet.aks.id]
    bypass                     = ["AzureServices"]
  }

  # Lifecycle management
  blob_properties {
    versioning_enabled = true

    delete_retention_policy {
      days = 30
    }
  }
}

resource "azurerm_storage_container" "bundles" {
  name                  = "bundles"
  storage_account_name  = azurerm_storage_account.bundles.name
  container_access_type = "private"
}

# Private endpoint for blob storage
resource "azurerm_private_endpoint" "blob" {
  name                = "signalbeam-blob-pe"
  location            = var.location
  resource_group_name = azurerm_resource_group.main.name
  subnet_id           = azurerm_subnet.private_endpoints.id

  private_service_connection {
    name                           = "signalbeam-blob-psc"
    private_connection_resource_id = azurerm_storage_account.bundles.id
    subresource_names              = ["blob"]
    is_manual_connection           = false
  }
}
```

**.NET Integration with Managed Identity:**
```csharp
// Use Azure Managed Identity (no connection string needed!)
services.AddSingleton<BlobServiceClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var storageUri = config["AzureBlobStorage:ServiceUri"];

    // Use Managed Identity in Azure, fallback to connection string locally
    if (Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") != null)
    {
        var credential = new DefaultAzureCredential();
        return new BlobServiceClient(new Uri(storageUri), credential);
    }
    else
    {
        var connectionString = config["AzureBlobStorage:ConnectionString"];
        return new BlobServiceClient(connectionString);
    }
});

// Usage
public class BundleStorageService
{
    private readonly BlobServiceClient _blobClient;
    private readonly string _containerName;

    public BundleStorageService(BlobServiceClient blobClient, IConfiguration config)
    {
        _blobClient = blobClient;
        _containerName = config["AzureBlobStorage:ContainerName"];
    }

    public async Task<string> UploadBundleAsync(string bundleId, Stream content)
    {
        var containerClient = _blobClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient($"{bundleId}.tar.gz");

        await blobClient.UploadAsync(content, overwrite: true);
        return blobClient.Uri.ToString();
    }
}
```

### Azure Managed Identity for Pods

**Workload Identity Configuration:**
```yaml
# Enable workload identity on AKS
apiVersion: v1
kind: ServiceAccount
metadata:
  name: device-manager
  namespace: signalbeam
  annotations:
    azure.workload.identity/client-id: "<managed-identity-client-id>"
    azure.workload.identity/tenant-id: "<tenant-id>"

---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: device-manager
  namespace: signalbeam
spec:
  template:
    metadata:
      labels:
        azure.workload.identity/use: "true"
    spec:
      serviceAccountName: device-manager
      containers:
        - name: device-manager
          image: signalbeam.azurecr.io/device-manager:latest
          env:
            - name: AZURE_CLIENT_ID
              value: "<managed-identity-client-id>"
            - name: AZURE_TENANT_ID
              value: "<tenant-id>"
```

**Managed Identity Permissions (Terraform):**
```hcl
# Create managed identity
resource "azurerm_user_assigned_identity" "device_manager" {
  name                = "device-manager-identity"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.location
}

# Grant blob access
resource "azurerm_role_assignment" "blob_contributor" {
  scope                = azurerm_storage_account.bundles.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_user_assigned_identity.device_manager.principal_id
}

# Grant Key Vault access
resource "azurerm_role_assignment" "keyvault_secrets" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.device_manager.principal_id
}

# Federated credential for workload identity
resource "azurerm_federated_identity_credential" "device_manager" {
  name                = "device-manager-federated-credential"
  resource_group_name = azurerm_resource_group.main.name
  parent_id           = azurerm_user_assigned_identity.device_manager.id
  audience            = ["api://AzureADTokenExchange"]
  issuer              = azurerm_kubernetes_cluster.main.oidc_issuer_url
  subject             = "system:serviceaccount:signalbeam:device-manager"
}
```

### Azure Container Registry (ACR)

**Setup with Geo-Replication:**
```hcl
resource "azurerm_container_registry" "main" {
  name                = "signalbeam${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.location
  sku                 = "Premium"  # Required for geo-replication
  admin_enabled       = false      # Use RBAC instead

  # Geo-replication for multi-region deployments
  georeplications {
    location                = "westeurope"
    zone_redundancy_enabled = true
  }

  georeplications {
    location                = "eastus"
    zone_redundancy_enabled = true
  }

  # Network access
  network_rule_set {
    default_action = "Deny"

    virtual_network {
      action    = "Allow"
      subnet_id = azurerm_subnet.aks.id
    }
  }

  # Security
  retention_policy {
    days    = 30
    enabled = true
  }

  trust_policy {
    enabled = true
  }
}

# Allow AKS to pull from ACR
resource "azurerm_role_assignment" "acr_pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_kubernetes_cluster.main.kubelet_identity[0].object_id
}
```

**GitHub Actions Integration:**
```yaml
# .github/workflows/build-push.yml
name: Build and Push to ACR

on:
  push:
    branches: [main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Azure Login
        uses: azure/login@v1
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: ACR Login
        run: az acr login --name signalbeam

      - name: Build and Push
        run: |
          docker build -t signalbeam.azurecr.io/device-manager:${{ github.sha }} .
          docker push signalbeam.azurecr.io/device-manager:${{ github.sha }}

      - name: Update Kustomize
        run: |
          cd deploy/kustomize/overlays/dev
          kustomize edit set image device-manager=signalbeam.azurecr.io/device-manager:${{ github.sha }}
          git commit -am "Update image to ${{ github.sha }}"
          git push
```

### Azure Monitor Integration

**Optional: Hybrid Observability (Prometheus + Azure Monitor):**
```hcl
# Log Analytics Workspace
resource "azurerm_log_analytics_workspace" "main" {
  name                = "signalbeam-logs-${var.environment}"
  location            = var.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = 90
}

# Azure Monitor workspace for Prometheus
resource "azurerm_monitor_workspace" "prometheus" {
  name                = "signalbeam-prometheus-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.location
}

# Azure Managed Grafana
resource "azurerm_dashboard_grafana" "main" {
  name                = "signalbeam-grafana-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.location

  identity {
    type = "SystemAssigned"
  }

  azure_monitor_workspace_integrations {
    resource_id = azurerm_monitor_workspace.prometheus.id
  }
}
```

**Forward Prometheus metrics to Azure Monitor:**
```yaml
# prometheus-remote-write-config.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: prometheus-config
  namespace: monitoring
data:
  prometheus.yml: |
    global:
      scrape_interval: 15s

    remote_write:
      - url: https://signalbeam-prometheus-dev.westeurope.prometheus.monitor.azure.com/api/v1/write
        azure_ad:
          managed_identity:
            client_id: "<managed-identity-client-id>"

    scrape_configs:
      - job_name: 'kubernetes-pods'
        kubernetes_sd_configs:
          - role: pod
```

## TimescaleDB for Time-Series Data

### Why TimescaleDB?
SignalBeam Edge collects massive amounts of time-series data from edge devices:
- Device heartbeats every 30 seconds
- Metrics (CPU, memory, disk) continuously
- Container status updates
- Telemetry streams

**TimescaleDB is PostgreSQL + time-series superpowers:**
- **10-100x faster** queries on time-series data
- **90% storage savings** with native compression
- **Automatic data management** with retention policies
- **Real-time aggregates** for dashboards
- **Full PostgreSQL compatibility** - use all existing EF Core code

### Setting Up TimescaleDB

**Enable Extension:**
```sql
CREATE EXTENSION IF NOT EXISTS timescaledb;
```

**Convert Tables to Hypertables:**
```sql
-- Device heartbeats (time-series)
SELECT create_hypertable('device_heartbeats', 'timestamp',
    chunk_time_interval => INTERVAL '1 day');

-- Device metrics (time-series)
SELECT create_hypertable('device_metrics', 'timestamp',
    chunk_time_interval => INTERVAL '1 day');

-- Add compression policy (compress data older than 7 days)
ALTER TABLE device_heartbeats SET (
    timescaledb.compress,
    timescaledb.compress_segmentby = 'device_id'
);

SELECT add_compression_policy('device_heartbeats', INTERVAL '7 days');

-- Add retention policy (drop data older than 90 days)
SELECT add_retention_policy('device_heartbeats', INTERVAL '90 days');
```

**Continuous Aggregates for Dashboards:**
```sql
-- Pre-compute hourly device metrics
CREATE MATERIALIZED VIEW device_metrics_hourly
WITH (timescaledb.continuous) AS
SELECT
    time_bucket('1 hour', timestamp) AS bucket,
    device_id,
    AVG(cpu_usage) as avg_cpu,
    AVG(memory_usage) as avg_memory,
    AVG(disk_usage) as avg_disk
FROM device_metrics
GROUP BY bucket, device_id;

-- Refresh policy (update every 30 minutes)
SELECT add_continuous_aggregate_policy('device_metrics_hourly',
    start_offset => INTERVAL '2 hours',
    end_offset => INTERVAL '30 minutes',
    schedule_interval => INTERVAL '30 minutes');
```

**EF Core Integration:**
```csharp
// Entities remain the same - just regular EF Core entities
public class DeviceHeartbeat
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public DateTime Timestamp { get; set; }
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double DiskUsage { get; set; }
}

// In migration, convert to hypertable
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql(@"
        SELECT create_hypertable('device_heartbeats', 'timestamp',
            chunk_time_interval => INTERVAL '1 day');
    ");
}

// Queries work exactly the same
var recentHeartbeats = await context.DeviceHeartbeats
    .Where(h => h.Timestamp > DateTime.UtcNow.AddHours(-24))
    .OrderByDescending(h => h.Timestamp)
    .ToListAsync();
```

## Valkey - Modern Redis Alternative

### Why Valkey?
- **Open Source:** BSD-3 license (Redis switched to SSPL - restrictive)
- **Industry Support:** Linux Foundation project backed by AWS, Google, Oracle
- **Drop-in Replacement:** 100% compatible with Redis API
- **Future-Proof:** Community-driven, no vendor lock-in

### Migration from Redis
**Zero code changes required!** Just point to Valkey instead of Redis:

```csharp
// Configuration stays the same
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = configuration["Valkey:ConnectionString"];
});

// Usage stays the same
var cache = serviceProvider.GetRequiredService<IDistributedCache>();
await cache.SetStringAsync("key", "value");
```

### Kubernetes Deployment
```bash
# Use Redis chart (Valkey is compatible)
helm install valkey bitnami/redis \
  --set image.repository=valkey/valkey \
  --set image.tag=7.2 \
  -n signalbeam
```

## Scalar - Modern API Documentation

### Why Scalar over Swashbuckle?
- **Beautiful Modern UI** - Much better developer experience
- **Interactive API Client** - Built-in, no need for Postman
- **Better Performance** - Faster rendering of large APIs
- **OpenAPI 3.1 Support** - Latest spec support
- **Dark Mode** - Native dark mode support

### Setup

**Install Package:**
```bash
dotnet add package Scalar.AspNetCore
```

**Configure in Program.cs:**
```csharp
// Replace Swashbuckle with Scalar
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // That's it! Much simpler than Swashbuckle
}

app.Run();
```

**Access at:** `http://localhost:5000/scalar/v1`

### Features
- **Try It Out:** Execute requests directly from the docs
- **Code Samples:** Auto-generated in multiple languages (curl, C#, Python, etc.)
- **Search:** Fast search across all endpoints
- **Themes:** Beautiful light and dark themes

## .NET Aspire for Local Development

### Why .NET Aspire?
- **Unified Dashboard:** See all services, logs, traces, metrics in one place
- **Service Discovery:** Built-in, no manual configuration
- **OpenTelemetry:** Automatic instrumentation for all services
- **Resource Management:** Start PostgreSQL, NATS, Valkey with one command
- **Cloud-Ready:** Same code works locally and in production

### Project Structure
```
src/
├── SignalBeam.AppHost/              # Aspire orchestrator
│   ├── Program.cs
│   └── SignalBeam.AppHost.csproj
│
├── SignalBeam.ServiceDefaults/      # Shared Aspire configuration
│   ├── Extensions.cs
│   └── SignalBeam.ServiceDefaults.csproj
│
└── DeviceManager/
    └── DeviceManager.Host/
        └── Program.cs               # Uses ServiceDefaults
```

### AppHost Configuration

**SignalBeam.AppHost/Program.cs:**
```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .AddDatabase("signalbeam");

var valkey = builder.AddRedis("valkey");

var nats = builder.AddContainer("nats", "nats", "latest")
    .WithArgs("--jetstream")
    .WithEndpoint(4222, targetPort: 4222, name: "nats");

var minio = builder.AddContainer("minio", "minio/minio", "latest")
    .WithArgs("server", "/data")
    .WithEndpoint(9000, targetPort: 9000, name: "api")
    .WithEndpoint(9001, targetPort: 9001, name: "console");

// Services
var deviceManager = builder.AddProject<Projects.DeviceManager_Host>("device-manager")
    .WithReference(postgres)
    .WithReference(valkey)
    .WithReference(nats);

var bundleOrchestrator = builder.AddProject<Projects.BundleOrchestrator_Host>("bundle-orchestrator")
    .WithReference(postgres)
    .WithReference(valkey)
    .WithReference(nats)
    .WithReference(minio);

builder.Build().Run();
```

**Service Integration (DeviceManager.Host/Program.cs):**
```csharp
var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (OpenTelemetry, health checks, etc.)
builder.AddServiceDefaults();

// Add PostgreSQL with EF Core
builder.AddNpgsqlDbContext<DeviceDbContext>("signalbeam");

// Add Valkey
builder.AddRedisDistributedCache("valkey");

var app = builder.Build();

app.MapDefaultEndpoints(); // Health, metrics

app.Run();
```

### Running Aspire
```bash
cd src/SignalBeam.AppHost
dotnet run
```

**Dashboard automatically opens at:** `http://localhost:15888`

**Features:**
- **Resources Tab:** See all running services
- **Console Logs:** Aggregated logs from all services
- **Traces:** Distributed tracing across all services
- **Metrics:** Real-time metrics visualization
- **Environment Variables:** Inspect configuration

## Cilium - eBPF-based Service Mesh

### Why Cilium?
Traditional service meshes (Istio, Linkerd) use sidecars (extra containers per pod):
- **Performance overhead:** 2x memory, CPU usage
- **Complexity:** More containers to manage

**Cilium uses eBPF (kernel-level):**
- **No sidecars:** Lower resource usage
- **Better performance:** Kernel-level networking
- **Unified:** CNI + Service Mesh + Network Policy in one
- **Observability:** Hubble provides deep network insights

### Features
- **Transparent mTLS:** Automatic encryption between services
- **L7 Policy:** HTTP-aware network policies
- **Load Balancing:** Advanced L4/L7 load balancing
- **Hubble UI:** Network flow visualization

### Installation
```bash
# Install Cilium CLI
curl -L --remote-name-all https://github.com/cilium/cilium-cli/releases/latest/download/cilium-linux-amd64.tar.gz
tar xzvf cilium-linux-amd64.tar.gz
sudo mv cilium /usr/local/bin/

# Install Cilium in Kubernetes
cilium install --version 1.14.5

# Enable Hubble (observability)
cilium hubble enable --ui

# Verify installation
cilium status
```

### Service Mesh Features
```bash
# Enable service mesh mode
cilium install \
  --set kubeProxyReplacement=strict \
  --set enableCiliumEndpointSlice=true \
  --set meshAuth.enabled=true

# Enable mTLS between services
kubectl annotate namespace signalbeam io.cilium.mesh=enabled
```

### Hubble Observability
```bash
# Port-forward Hubble UI
cilium hubble ui

# Access at http://localhost:12000

# CLI observability
hubble observe --namespace signalbeam
hubble observe --from-pod device-manager --protocol http
```

## Zitadel - Modern Identity Platform

### Why Zitadel?
**vs Keycloak:**
- **Cloud-Native:** Built for Kubernetes from day one
- **Simpler:** Easier to configure and operate
- **Better APIs:** gRPC and REST, API-first design
- **Modern Stack:** Written in Go, not Java
- **Multi-Tenancy:** Built-in, not bolted on

### Deployment
```bash
# Install with Helm
helm repo add zitadel https://charts.zitadel.com
helm install zitadel zitadel/zitadel \
  --namespace signalbeam \
  --set zitadel.masterkey="..." \
  --set zitadel.configmapConfig.ExternalSecure=true \
  --set zitadel.configmapConfig.ExternalDomain=auth.signalbeam.com
```

### .NET Integration
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://auth.signalbeam.com";
        options.Audience = "signalbeam-api";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true
        };
    });
```

### Features
- **Multi-Factor Auth:** TOTP, WebAuthn, SMS
- **Passwordless:** FIDO2, Passkeys support
- **Actions:** Custom logic (like Auth0 Actions)
- **Branding:** Fully customizable login UI
- **Audit Log:** Complete audit trail

## OpenObserve - Next-Gen Observability

### Why OpenObserve?
**vs Grafana Loki:**
- **140x cheaper storage:** Aggressive compression
- **10x faster queries:** Written in Rust
- **Simpler operations:** Single binary, no dependencies
- **Unified:** Logs + Metrics + Traces in one platform
- **S3-native:** Direct S3 storage, no local disk needed

### Deployment
```bash
# Install with Helm
helm repo add openobserve https://charts.openobserve.ai
helm install openobserve openobserve/openobserve \
  --namespace signalbeam \
  --set persistence.enabled=true \
  --set persistence.size=50Gi
```

### .NET Integration
```csharp
// Serilog sink for OpenObserve
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .WriteTo.Http(
            requestUri: "http://openobserve:5080/api/default/logs",
            queueLimitBytes: null,
            httpClient: new HttpClient
            {
                DefaultRequestHeaders =
                {
                    Authorization = new AuthenticationHeaderValue("Basic",
                        Convert.ToBase64String(Encoding.ASCII.GetBytes("admin:password")))
                }
            });
});
```

### Features
- **Logs:** High-performance log ingestion and search
- **Metrics:** Prometheus-compatible metrics
- **Traces:** OpenTelemetry traces
- **Alerts:** Built-in alerting
- **Dashboards:** Grafana-compatible dashboards

## NATS Messaging Architecture

### Why NATS?
- **Lightweight:** Minimal resource footprint, ideal for edge and cloud deployments
- **Cloud-Native:** Built for Kubernetes with native clustering support
- **Flexible:** Core NATS for pub/sub, JetStream for persistence, KV for config, Object Store for artifacts
- **Simple:** Easy to operate, no external dependencies for clustering
- **Fast:** High-throughput, low-latency messaging

### NATS Components

#### Core NATS (Pub/Sub)
Use for ephemeral, fire-and-forget messages:
- Device status updates (real-time, loss tolerant)
- Transient alerts
- Metrics that are immediately written to TimescaleDB/Prometheus

#### JetStream (Persistent Messaging)
Use for critical events that must not be lost:
- Device registration events
- Bundle assignment changes
- Device lifecycle events (created, updated, deleted)
- Rollout status updates

#### Subject Hierarchy
```
signalbeam.
├── devices.
│   ├── heartbeat.<deviceId>          # Core NATS: ephemeral heartbeats
│   ├── events.<eventType>            # JetStream: device lifecycle events
│   ├── commands.<deviceId>           # Request/Reply: send commands to agent
│   └── status.<deviceId>             # JetStream: status changes
├── bundles.
│   ├── assignments.<deviceId>        # JetStream: bundle assignments
│   ├── rollouts.<rolloutId>          # JetStream: rollout progress
│   └── artifacts.uploaded            # JetStream: artifact upload notifications
└── telemetry.
    ├── metrics.<deviceId>            # Core NATS: high-frequency metrics
    └── logs.<deviceId>               # Core NATS: log streaming
```

### JetStream Streams Configuration

**Device Events Stream:**
```json
{
  "name": "DEVICE_EVENTS",
  "subjects": ["signalbeam.devices.events.>"],
  "retention": "limits",
  "max_age": 2592000000000000,  // 30 days in nanoseconds
  "storage": "file",
  "num_replicas": 3
}
```

**Bundle Assignments Stream:**
```json
{
  "name": "BUNDLE_ASSIGNMENTS",
  "subjects": ["signalbeam.bundles.assignments.>"],
  "retention": "limits",
  "max_age": 7776000000000000,  // 90 days
  "storage": "file",
  "num_replicas": 3
}
```

### NATS Integration Pattern in .NET

```csharp
// Infrastructure/Messaging/NatsMessagePublisher.cs
public class NatsMessagePublisher : IMessagePublisher
{
    private readonly IConnection _connection;
    private readonly IJetStream _jetStream;

    public async Task PublishAsync<T>(string subject, T message, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(message);
        var data = Encoding.UTF8.GetBytes(json);

        // Use JetStream for persistent messages
        if (subject.Contains(".events.") || subject.Contains(".assignments."))
        {
            var ack = await _jetStream.PublishAsync(subject, data, cancellationToken: ct);
            // Handle acknowledgment
        }
        else
        {
            // Use Core NATS for ephemeral messages
            await _connection.PublishAsync(subject, data, cancellationToken: ct);
        }
    }
}

// Infrastructure/Messaging/NatsMessageSubscriber.cs
public class DeviceEventsSubscriber : BackgroundService
{
    private readonly IJetStreamPushAsyncSubscription _subscription;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var msg in _subscription.Msgs.ReadAllAsync(stoppingToken))
        {
            try
            {
                var @event = JsonSerializer.Deserialize<DeviceEvent>(msg.Data);
                await _handler.HandleAsync(@event, stoppingToken);
                await msg.AckAsync(cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                await msg.NakAsync(delay: TimeSpan.FromSeconds(5), cancellationToken: stoppingToken);
            }
        }
    }
}
```

### NATS in Kubernetes

**Deployment via Helm:**
```bash
# Add NATS Helm repo
helm repo add nats https://nats-io.github.io/k8s/helm/charts/

# Install NATS with JetStream
helm install nats nats/nats \
  --namespace signalbeam \
  --set nats.jetstream.enabled=true \
  --set nats.jetstream.fileStorage.size=10Gi \
  --set cluster.enabled=true \
  --set cluster.replicas=3
```

**Service Discovery:**
```
nats://nats.signalbeam.svc.cluster.local:4222
```

## ArgoCD GitOps Workflow

### Why ArgoCD?
- **Declarative:** Kubernetes manifests stored in Git as source of truth
- **Automated:** Continuous synchronization from Git to cluster
- **Auditable:** Full history of deployments via Git commits
- **Rollback:** Easy rollback to any previous Git commit
- **Multi-Environment:** Manage dev, staging, prod from single tool

### Repository Structure for ArgoCD

```
signalbeam-edge/
├── deploy/
│   ├── argocd/
│   │   ├── applications/              # ArgoCD Application manifests
│   │   │   ├── dev/
│   │   │   │   ├── device-manager.yaml
│   │   │   │   ├── bundle-orchestrator.yaml
│   │   │   │   └── infrastructure.yaml
│   │   │   ├── staging/
│   │   │   └── prod/
│   │   │
│   │   └── projects/                  # ArgoCD AppProjects
│   │       └── signalbeam.yaml
│   │
│   └── kustomize/
│       ├── base/                      # Base Kubernetes manifests
│       │   ├── device-manager/
│       │   │   ├── deployment.yaml
│       │   │   ├── service.yaml
│       │   │   ├── configmap.yaml
│       │   │   └── kustomization.yaml
│       │   └── bundle-orchestrator/
│       │
│       └── overlays/                  # Environment-specific patches
│           ├── dev/
│           │   ├── device-manager/
│           │   │   ├── kustomization.yaml
│           │   │   └── patches.yaml
│           │   └── namespace.yaml
│           ├── staging/
│           └── prod/
```

### ArgoCD Application Example

```yaml
# deploy/argocd/applications/dev/device-manager.yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: device-manager-dev
  namespace: argocd
spec:
  project: signalbeam

  source:
    repoURL: https://github.com/your-org/signalbeam-edge.git
    targetRevision: main
    path: deploy/kustomize/overlays/dev/device-manager

  destination:
    server: https://kubernetes.default.svc
    namespace: signalbeam-dev

  syncPolicy:
    automated:
      prune: true      # Remove resources deleted from Git
      selfHeal: true   # Auto-sync if cluster state drifts
    syncOptions:
      - CreateNamespace=true
    retry:
      limit: 5
      backoff:
        duration: 5s
        factor: 2
        maxDuration: 3m

  # Health assessment
  ignoreDifferences:
    - group: apps
      kind: Deployment
      jsonPointers:
        - /spec/replicas  # Ignore if HPA manages replicas
```

### ArgoCD AppProject

```yaml
# deploy/argocd/projects/signalbeam.yaml
apiVersion: argoproj.io/v1alpha1
kind: AppProject
metadata:
  name: signalbeam
  namespace: argocd
spec:
  description: SignalBeam Edge Platform

  sourceRepos:
    - https://github.com/your-org/signalbeam-edge.git

  destinations:
    - namespace: 'signalbeam-*'
      server: https://kubernetes.default.svc

  clusterResourceWhitelist:
    - group: ''
      kind: Namespace
    - group: ''
      kind: PersistentVolume

  namespaceResourceWhitelist:
    - group: '*'
      kind: '*'
```

### Kustomize Overlay Example

```yaml
# deploy/kustomize/overlays/dev/device-manager/kustomization.yaml
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization

namespace: signalbeam-dev

bases:
  - ../../../base/device-manager

images:
  - name: signalbeam/device-manager
    newTag: dev-abc123  # Updated by CI pipeline

replicas:
  - name: device-manager
    count: 2

configMapGenerator:
  - name: device-manager-config
    behavior: merge
    literals:
      - ASPNETCORE_ENVIRONMENT=Development
      - LOG_LEVEL=Debug

patches:
  - path: patches.yaml
```

### CI/CD Pipeline with ArgoCD

**GitHub Actions Example:**
```yaml
# .github/workflows/deploy.yml
name: Deploy to Dev

on:
  push:
    branches: [main]

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Build and push Docker image
        run: |
          docker build -t ghcr.io/org/device-manager:${{ github.sha }} .
          docker push ghcr.io/org/device-manager:${{ github.sha }}

      - name: Update Kustomize image tag
        run: |
          cd deploy/kustomize/overlays/dev/device-manager
          kustomize edit set image signalbeam/device-manager=ghcr.io/org/device-manager:${{ github.sha }}

      - name: Commit and push changes
        run: |
          git config user.name "github-actions[bot]"
          git config user.email "github-actions[bot]@users.noreply.github.com"
          git add deploy/kustomize/overlays/dev/device-manager/kustomization.yaml
          git commit -m "Update dev image to ${{ github.sha }}"
          git push

      # ArgoCD will automatically detect the change and sync
```

### ArgoCD CLI Commands

```bash
# Login to ArgoCD
argocd login argocd.signalbeam.com

# Create application
argocd app create device-manager-dev \
  --repo https://github.com/your-org/signalbeam-edge.git \
  --path deploy/kustomize/overlays/dev/device-manager \
  --dest-server https://kubernetes.default.svc \
  --dest-namespace signalbeam-dev

# Sync application
argocd app sync device-manager-dev

# Get application status
argocd app get device-manager-dev

# List applications
argocd app list

# Rollback to previous version
argocd app rollback device-manager-dev <revision>

# View application history
argocd app history device-manager-dev

# Delete application
argocd app delete device-manager-dev
```

## Terragrunt Structure & Best Practices

### Why Terragrunt?
- **DRY Configuration:** Avoid repeating Terraform backend, provider, and variable configurations
- **Dependency Management:** Explicit dependencies between modules with automatic ordering
- **Multiple Environments:** Easily manage dev, staging, prod with shared modules
- **State Management:** Automatic S3 backend configuration with locking

### Directory Structure
```
infra/
├── terraform/modules/              # Reusable Terraform modules
│   ├── aks-cluster/               # AKS with managed identity, monitoring
│   ├── networking/                # VNet, subnets, NSGs, private DNS
│   ├── postgresql/                # Azure Database for PostgreSQL + TimescaleDB
│   ├── storage/                   # Azure Storage Account + containers
│   ├── container-registry/        # Azure Container Registry
│   ├── monitoring/                # Log Analytics, Azure Monitor, Grafana
│   ├── key-vault/                 # Azure Key Vault for secrets
│   └── managed-identity/          # User-assigned managed identities
│
└── terragrunt/                     # Live infrastructure configurations
    ├── terragrunt.hcl              # Root config (shared by all environments)
    ├── dev/
    │   ├── terragrunt.hcl          # Dev environment config
    │   ├── k8s-cluster/
    │   │   └── terragrunt.hcl      # References ../../../terraform/modules/k8s-cluster
    │   ├── database/
    │   │   └── terragrunt.hcl      # Depends on k8s-cluster
    │   └── storage/
    │       └── terragrunt.hcl
    └── prod/
        └── ...
```

### Root terragrunt.hcl Example (Azure)
```hcl
# Generate backend configuration for all child modules
remote_state {
  backend = "azurerm"
  generate = {
    path      = "backend.tf"
    if_exists = "overwrite"
  }
  config = {
    resource_group_name  = "signalbeam-tfstate-rg"
    storage_account_name = "signalbeamtfstate${get_env("ENVIRONMENT", "dev")}"
    container_name       = "terraform-state"
    key                  = "${path_relative_to_include()}/terraform.tfstate"
  }
}

# Generate provider configuration for Azure
generate "provider" {
  path      = "provider.tf"
  if_exists = "overwrite"
  contents  = <<EOF
terraform {
  required_version = ">= 1.5"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.85"
    }
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 2.47"
    }
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.23"
    }
    helm = {
      source  = "hashicorp/helm"
      version = "~> 2.12"
    }
  }
}

provider "azurerm" {
  features {
    resource_group {
      prevent_deletion_if_contains_resources = false
    }
    key_vault {
      purge_soft_delete_on_destroy = true
    }
  }
}
EOF
}
```

### Environment-specific terragrunt.hcl (Azure)
```hcl
# infra/terragrunt/dev/aks-cluster/terragrunt.hcl
terraform {
  source = "../../../terraform/modules//aks-cluster"
}

include "root" {
  path = find_in_parent_folders()
}

# Dependencies
dependency "networking" {
  config_path = "../networking"

  mock_outputs = {
    vnet_id              = "/subscriptions/mock/resourceGroups/mock/providers/Microsoft.Network/virtualNetworks/mock"
    aks_subnet_id        = "/subscriptions/mock/resourceGroups/mock/providers/Microsoft.Network/virtualNetworks/mock/subnets/aks"
    db_subnet_id         = "/subscriptions/mock/resourceGroups/mock/providers/Microsoft.Network/virtualNetworks/mock/subnets/db"
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan", "init"]
}

inputs = {
  cluster_name        = "signalbeam-dev-aks"
  resource_group_name = "signalbeam-dev-rg"
  location            = "westeurope"
  environment         = "dev"

  # From networking dependency
  vnet_subnet_id = dependency.networking.outputs.aks_subnet_id

  # Node pools
  system_node_pool = {
    vm_size             = "Standard_D4s_v5"
    node_count          = 3
    availability_zones  = [1, 2, 3]
  }

  user_node_pools = {
    workload = {
      vm_size             = "Standard_D8s_v5"
      min_count           = 2
      max_count           = 10
      availability_zones  = [1, 2, 3]
    }
  }

  tags = {
    Environment = "dev"
    ManagedBy   = "terragrunt"
    Project     = "signalbeam"
  }
}
```

### Dependency Management (Azure)
```hcl
# infra/terragrunt/dev/database/terragrunt.hcl
terraform {
  source = "../../../terraform/modules//postgresql"
}

include "root" {
  path = find_in_parent_folders()
}

dependency "networking" {
  config_path = "../networking"

  mock_outputs = {
    vnet_id       = "/subscriptions/mock/resourceGroups/mock/providers/Microsoft.Network/virtualNetworks/mock"
    db_subnet_id  = "/subscriptions/mock/resourceGroups/mock/providers/Microsoft.Network/virtualNetworks/mock/subnets/db"
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan", "init"]
}

inputs = {
  server_name         = "signalbeam-dev-db"
  resource_group_name = "signalbeam-dev-rg"
  location            = "westeurope"
  environment         = "dev"

  # From networking dependency
  delegated_subnet_id = dependency.networking.outputs.db_subnet_id

  # Database configuration
  sku_name   = "GP_Standard_D4s_v3"
  storage_mb = 131072

  # Enable TimescaleDB
  enable_timescaledb = true

  tags = {
    Environment = "dev"
    ManagedBy   = "terragrunt"
    Project     = "signalbeam"
  }
}
```

## Helm Chart Structure

Each microservice has its own Helm chart following this structure:

```
src/DeviceManager/charts/
├── Chart.yaml                      # Chart metadata
├── values.yaml                     # Default values
├── values-dev.yaml                 # Dev environment overrides
├── values-prod.yaml                # Prod environment overrides
└── templates/
    ├── deployment.yaml             # Kubernetes Deployment
    ├── service.yaml                # Kubernetes Service
    ├── ingress.yaml                # Ingress (optional)
    ├── configmap.yaml              # ConfigMap for configuration
    ├── secret.yaml                 # Secret for sensitive data
    ├── hpa.yaml                    # HorizontalPodAutoscaler
    ├── servicemonitor.yaml         # Prometheus ServiceMonitor
    └── _helpers.tpl                # Template helpers
```

### Key Helm Values
```yaml
# values.yaml
replicaCount: 2

image:
  repository: ghcr.io/signalbeam/device-manager
  tag: latest
  pullPolicy: IfNotPresent

service:
  type: ClusterIP
  port: 80
  targetPort: 8080

resources:
  requests:
    cpu: 100m
    memory: 128Mi
  limits:
    cpu: 500m
    memory: 512Mi

autoscaling:
  enabled: true
  minReplicas: 2
  maxReplicas: 10
  targetCPUUtilizationPercentage: 70

probes:
  liveness:
    path: /health/live
    initialDelaySeconds: 30
  readiness:
    path: /health/ready
    initialDelaySeconds: 10

env:
  - name: ASPNETCORE_ENVIRONMENT
    value: "Production"
  - name: ConnectionStrings__DefaultConnection
    valueFrom:
      secretKeyRef:
        name: device-manager-secrets
        key: db-connection

monitoring:
  enabled: true
  path: /metrics
```

## Local Development with Tilt

Tilt enables fast local Kubernetes development with live updates.

### Tiltfile Example
```python
# Load Kubernetes YAML
k8s_yaml(['deploy/kustomize/dev'])

# Build and deploy DeviceManager
docker_build('signalbeam/device-manager', './src/DeviceManager',
  dockerfile='./src/DeviceManager/DeviceManager.Host/Dockerfile',
  live_update=[
    sync('./src/DeviceManager', '/app/src/DeviceManager'),
    run('dotnet build /app/src/DeviceManager/DeviceManager.Host',
        trigger=['./src/DeviceManager/**/*.cs', './src/DeviceManager/**/*.csproj'])
  ]
)

# Deploy with Helm
k8s_resource('device-manager',
  port_forwards=['8080:80'],
  resource_deps=['postgresql', 'nats', 'valkey']
)

# Infrastructure dependencies
helm_resource('postgresql', 'bitnami/postgresql',
  namespace='signalbeam',
  flags=['--set', 'auth.database=signalbeam']
)

helm_resource('nats', 'nats/nats',
  namespace='signalbeam',
  flags=[
    '--set', 'nats.jetstream.enabled=true',
    '--set', 'nats.jetstream.fileStorage.size=5Gi'
  ]
)

helm_resource('valkey', 'bitnami/redis',
  namespace='signalbeam',
  flags=[
    '--set', 'image.repository=valkey/valkey',
    '--set', 'image.tag=7.2',
    '--set', 'auth.enabled=false'  # For local dev only
  ]
)
```

## Azure Deployment Quick Reference

### Initial Azure Setup

**1. Create Resource Groups:**
```bash
# Terraform state storage
az group create --name signalbeam-tfstate-rg --location westeurope

# Dev environment
az group create --name signalbeam-dev-rg --location westeurope
```

**2. Create Terraform State Storage:**
```bash
# Create storage account for Terraform state
az storage account create \
  --name signalbeamtfstatedev \
  --resource-group signalbeam-tfstate-rg \
  --location westeurope \
  --sku Standard_LRS \
  --encryption-services blob

# Create container
az storage container create \
  --name terraform-state \
  --account-name signalbeamtfstatedev
```

**3. Deploy Infrastructure with Terragrunt:**
```bash
cd infra/terragrunt/dev

# Initialize and deploy everything
terragrunt run-all init
terragrunt run-all plan
terragrunt run-all apply

# Or deploy sequentially
cd networking && terragrunt apply
cd ../aks-cluster && terragrunt apply
cd ../database && terragrunt apply
cd ../storage && terragrunt apply
cd ../container-registry && terragrunt apply
```

**4. Configure kubectl for AKS:**
```bash
# Get AKS credentials
az aks get-credentials \
  --resource-group signalbeam-dev-rg \
  --name signalbeam-dev-aks

# Verify connection
kubectl get nodes

# Install kubelogin for Azure AD authentication
kubelogin convert-kubeconfig -l azurecli
```

**5. Deploy Platform with ArgoCD:**
```bash
# Install ArgoCD
kubectl create namespace argocd
kubectl apply -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml

# Access ArgoCD UI
kubectl port-forward svc/argocd-server -n argocd 8080:443

# Get initial password
kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath="{.data.password}" | base64 -d

# Apply ArgoCD applications
kubectl apply -f deploy/argocd/applications/dev/
```

### Azure Resource Naming Convention

```
{service}-{environment}-{resource-type}

Examples:
- signalbeam-dev-aks (AKS cluster)
- signalbeam-dev-db (PostgreSQL server)
- signalbeam-prod-rg (Resource group)
- signalbeamdevacr (ACR - no hyphens, lowercase only)
- signalbeamdevstorage (Storage account - no hyphens, lowercase only)
```

### Azure Cost Optimization Tips

**Development Environment:**
- Use **B-series VMs** for non-production (burstable)
- Enable **auto-shutdown** for dev VMs
- Use **Azure Dev/Test pricing** (requires Visual Studio subscription)
- Set **retention policies** to 7 days for dev logs

**Production Environment:**
- Use **Reserved Instances** (1 or 3-year commitment) for 40-60% savings
- Enable **Azure Hybrid Benefit** if you have Windows Server licenses
- Use **Spot VMs** for non-critical workloads (up to 90% discount)
- Configure **autoscaling** to scale down during off-hours
- Use **Azure Monitor** cost analysis to identify expensive resources

### Azure Security Checklist

- [ ] Enable **Private Endpoints** for all PaaS services (PostgreSQL, Storage, ACR)
- [ ] Use **Managed Identities** instead of connection strings/keys
- [ ] Enable **Azure AD Workload Identity** for AKS pods
- [ ] Configure **Network Security Groups (NSGs)** to restrict traffic
- [ ] Enable **Azure Policy** to enforce organizational standards
- [ ] Use **Azure Key Vault** for secrets management
- [ ] Enable **diagnostic settings** for audit logging
- [ ] Configure **Azure Front Door WAF** for web application protection
- [ ] Enable **Microsoft Defender for Cloud** for security recommendations
- [ ] Set up **Azure AD Conditional Access** for admin access

## Future Roadmap (post-MVP)

These features inform design decisions but are not implemented in v0.1:
- Per-device certificates and mTLS
- RBAC for multi-user teams
- Rules engine for edge sensor data
- AI model distribution and versioning
- Canary updates and automatic rollbacks
- Advanced telemetry and log aggregation
- **Azure IoT Hub integration** for device provisioning and management
- **Azure Arc** for hybrid edge device management
- **Azure AI Services** for edge AI model deployment
