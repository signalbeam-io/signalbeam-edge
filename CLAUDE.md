# CLAUDE.md

## Project Overview

SignalBeam Edge is a fleet management platform for edge devices (Raspberry Pis, mini-PCs) that enables onboarding, monitoring, and updating containerized applications from a centralized dashboard. Manage N edge devices running Docker containers without SSH.

## Tech Stack

- **Backend:** .NET 10, C# 14, hexagonal architecture
- **CQRS/Messaging:** WolverineFx, NATS with JetStream
- **Data:** EF Core + PostgreSQL/TimescaleDB, Dapper for read queries
- **Cache:** Valkey (Redis-compatible, uses StackExchange.Redis client)
- **Storage:** Azure Blob Storage for bundle artifacts
- **Auth:** Microsoft Entra ID (JWT/OIDC), API keys for MVP
- **Observability:** OpenTelemetry, Serilog, Prometheus, Grafana/Loki/Tempo
- **Resilience:** Polly (retry, circuit breaker, timeout)
- **Validation/Mapping:** FluentValidation, Riok.Mapperly (source gen)
- **API Docs:** Scalar.AspNetCore (replaces Swashbuckle)
- **Testing:** xUnit, FluentAssertions, NSubstitute, Testcontainers, WireMock.Net, NetArchTest.Rules
- **Frontend:** React 18+, TypeScript, shadcn/ui, TanStack Query, Zustand, Vite, React Router v6
- **Infra:** AKS, Helm, Terraform + Terragrunt, ArgoCD, GitHub Actions
- **Local Dev:** .NET Aspire (AppHost) or Docker Compose

## Repository Structure

```
src/
├── SignalBeam.sln
├── Directory.Build.props              # Nullable refs, warnings as errors, analyzers
├── Directory.Packages.props           # Central NuGet version management
├── SignalBeam.AppHost/                # .NET Aspire orchestrator
├── SignalBeam.ServiceDefaults/        # Shared OpenTelemetry, health checks
├── SignalBeam.Domain/                 # Shared domain (Entities, ValueObjects, Events, Abstractions)
├── SignalBeam.Shared.Infrastructure/  # Shared infra (Auth, Results, Messaging, Resilience, Time)
├── SignalBeam.DeviceManager/          # Microservice: device registration & state
├── BundleOrchestrator/                # Microservice: bundle management & rollouts
├── TelemetryProcessor/                # Microservice: metrics & heartbeat processing
└── SignalBeam.EdgeAgent/              # Edge device agent (console app)

web/          # React + TypeScript frontend
deploy/       # Helm charts & Kustomize overlays
infra/        # Terraform + Terragrunt
tests/        # Domain, integration, E2E tests
```

## Microservice Layer Structure

Each microservice follows hexagonal architecture:

```
ServiceName/
├── ServiceName.Application/       # Commands/, Queries/, Events/, Validators/, Contracts/
├── ServiceName.Infrastructure/    # Persistence/ (EF Core), ExternalServices/, DI registration
└── ServiceName.Host/              # Program.cs, Endpoints/ (minimal API), Middleware/
```

**Layer rules (enforced by NetArchTest):**
- Domain has zero dependencies on Infrastructure or Host
- Application doesn't reference Host
- Endpoints only depend on Application layer

## Architecture Patterns

- **CQRS:** Commands (state changes) and Queries (reads) via Wolverine handlers
- **Result pattern:** Return `Result<T>` instead of throwing exceptions for business logic
- **Event-driven:** Domain events published on state changes, processed async
- **Repository pattern:** Interfaces in Domain, EF Core impl in Infrastructure, Dapper for reads
- **Desired state model:** Cloud stores desired bundle per device; agent polls and reconciles

## Key Domain Concepts

- **App Bundles:** Named, versioned sets of container definitions to deploy on devices
- **Device Groups:** Logical groupings by tags (`lab`, `prod`, `rpi`); bundles assigned to groups propagate to members
- **Reconciliation:** Agent polls desired state, compares with running containers, pulls/starts/stops to match
- **Rollout tracking:** Pending -> Updating -> Succeeded/Failed

## NATS Subject Hierarchy

```
signalbeam.devices.heartbeat.<deviceId>      # Core NATS (ephemeral)
signalbeam.devices.events.<eventType>        # JetStream (persistent)
signalbeam.devices.commands.<deviceId>       # Request/Reply
signalbeam.devices.status.<deviceId>         # JetStream
signalbeam.bundles.assignments.<deviceId>    # JetStream
signalbeam.bundles.rollouts.<rolloutId>      # JetStream
signalbeam.telemetry.metrics.<deviceId>      # Core NATS (ephemeral)
```

## Common Commands

```bash
# Build & test
dotnet build src/SignalBeam.sln
dotnet test src/SignalBeam.sln
dotnet test --filter Category=Integration   # requires Docker
dotnet format src/SignalBeam.sln

# Run locally with Aspire
cd src/SignalBeam.AppHost && dotnet run      # dashboard at localhost:15888

# EF Core migrations (from Infrastructure project)
dotnet ef migrations add <Name>
dotnet ef database update

# Frontend
cd web && npm install && npm run dev
cd web && npm test && npm run lint

# Helm (one chart per service under deploy/charts/)
for c in device-manager bundle-orchestrator telemetry-processor identity-manager api-gateway web; do
  helm upgrade --install "$c" "deploy/charts/$c" -n signalbeam --create-namespace
done

# Azure Container Apps (lean ~$20/mo path) — see infra/terragrunt/dev/aca/README.md
cd infra/terragrunt/dev && terragrunt run --all apply --working-dir ./aca
```

## Code Quality

Enforced via `Directory.Build.props`:
- Nullable reference types enabled
- Treat warnings as errors
- Roslynator + SonarAnalyzer analyzers
- Central package management via `Directory.Packages.props` (no versions in .csproj files)

## Development Workflow

1. Feature branch from `main`
2. Domain modeling first (entities, value objects)
3. Application logic (command/query handlers)
4. Infrastructure (repositories, external integrations)
5. Endpoints (minimal API in Host)
6. Tests (unit + integration with Testcontainers)
7. PR with tests passing

## MVP Scope (v0.1)

**In:** Device registration, heartbeats, health metrics, grouping/tagging, bundle CRUD & versioning, bundle assignment, agent reconciliation, rollout tracking, event log.

**Out:** AI/ML, mTLS/PKI, RBAC UI, canary/blue-green rollouts, air-gapped deployments, advanced telemetry UI.
