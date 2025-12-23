# SignalBeam Edge

> Fleet management platform for edge devices that eliminates manual SSH access

SignalBeam Edge is a platform for onboarding, monitoring, and updating fleets of edge devices (e.g., Raspberry Pis, mini-PCs running containers) from a single dashboard. It targets small-to-medium fleets that need simple, reliable rollout and visibility without heavyweight IoT stacks.

## 🎯 Product Focus

- **Radical simplicity**: device → group → bundle → status
- **First-class fleet visibility** with clear rollout outcomes
- **Built for teams** managing ~5–200 devices without an IoT platform team
- **Open, opinionated stack** that integrates with existing tooling

See [docs/signalbeam-edge-business-plan.md](docs/signalbeam-edge-business-plan.md) for the full market rationale and MVP scope.

## 🏗️ Architecture at a Glance

### Core Components

| Component | Path | Description |
|-----------|------|-------------|
| **Edge Agent** | `src/EdgeAgent/` | Runs on devices, handles registration, heartbeats, bundle reconciliation, and status reporting |
| **DeviceManager** | `src/DeviceManager/` | Core API for device identity, grouping, status, and bundle assignment |
| **BundleOrchestrator** | `src/BundleOrchestrator/` | Coordinates bundle rollouts and deployment state tracking |
| **TelemetryProcessor** | `src/TelemetryProcessor/` | Ingests and processes metrics/telemetry from devices |
| **Web UI** | `web/` | React + TypeScript dashboard for fleet management |

### Infrastructure Stack

- **Database**: PostgreSQL with TimescaleDB for time-series data
- **Cache**: Valkey (Redis-compatible)
- **Messaging**: NATS with JetStream for event streaming
- **Storage**: Azure Blob Storage for bundle artifacts
- **Orchestration**: .NET Aspire for local development

## 🚀 Quick Start

### Prerequisites

- **.NET 9 SDK** ([download](https://dotnet.microsoft.com/download/dotnet/9.0))
- **Docker Desktop** (for infrastructure services)
- **Node.js 20+** (for frontend)
- **.NET Aspire workload**:
  ```bash
  dotnet workload install aspire
  ```

### Run the Full Stack (Recommended)

The easiest way to run the entire SignalBeam platform locally:

```bash
# Navigate to the Aspire AppHost
cd src/SignalBeam.AppHost

# Start all services
dotnet run
```

This will:
- Start PostgreSQL, Valkey, NATS, and Azure Storage (Azurite)
- Launch all backend microservices
- Open the Aspire Dashboard at `http://localhost:15888`

**Aspire Dashboard provides**:
- Service logs and traces
- Metrics visualization
- Service health status
- Service URLs and endpoints

### Run the Frontend

In a separate terminal:

```bash
cd web
npm install
npm run dev
```

The web UI will be available at `http://localhost:5173`

### Run Individual Services

To run a single service (requires infrastructure to be running):

```bash
cd src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Host
dotnet run
```

## 📖 Core Workflows (MVP)

### 1. Device Registration
- Edge agent registers device with tenant + device ID
- Device appears in the web dashboard
- Agent begins sending heartbeats

### 2. Heartbeat & Health Monitoring
- Agent sends periodic health metrics (CPU, memory, disk)
- Dashboard displays online/offline status
- Historical metrics stored in TimescaleDB

### 3. Bundle Management
- Create bundles with container definitions
- Version bundles for controlled releases
- Assign bundles to devices or groups

### 4. Rollout Orchestration
- User initiates rollout to target devices/groups
- **Real-time tracking** of rollout progress
- **Device-level status**: pending → updating → succeeded/failed
- **Automatic retries** for failed devices
- **Rollout cancellation** for active deployments

### 5. Device Reconciliation
- Agent pulls desired bundle configuration
- Automatically starts/stops containers to match desired state
- Reports deployment status and errors back to cloud

## 🎨 Features

### ✅ Implemented (MVP)

- [x] Device registration and authentication
- [x] Device grouping and tagging
- [x] Heartbeat monitoring with health metrics
- [x] Bundle creation and versioning
- [x] Bundle assignment to devices/groups
- [x] **Rollout status tracking with real-time updates**
- [x] **Device-level rollout progress visualization**
- [x] **Rollout cancellation and retry mechanisms**
- [x] Container reconciliation on edge devices
- [x] Fleet dashboard with status overview
- [x] Responsive web UI
- [x] API key authentication for devices
- [x] JWT authentication for users (ready for OIDC)

### 🚧 In Progress / Planned

- [ ] Advanced rollout strategies (canary, blue/green)
- [ ] Device-level logs aggregation
- [ ] Alert rules and notifications
- [ ] RBAC for multi-user teams
- [ ] Prometheus metrics export
- [ ] Grafana dashboard templates
- [ ] mTLS for device-to-cloud communication
- [ ] Air-gapped deployment support

## 🛠️ Technology Stack

### Backend (.NET 9)
- **Framework**: .NET 9, C# 14, ASP.NET Core Minimal APIs
- **Architecture**: Hexagonal (Ports & Adapters) with CQRS
- **Database**: Entity Framework Core + PostgreSQL + TimescaleDB
- **Messaging**: NATS with JetStream
- **Storage**: Azure Blob Storage (Azurite locally)
- **Observability**: OpenTelemetry, Serilog, Prometheus
- **Validation**: FluentValidation
- **Testing**: xUnit, FluentAssertions, Testcontainers

### Frontend (React + TypeScript)
- **Framework**: React 18, TypeScript, Vite
- **UI Library**: shadcn/ui (Tailwind CSS-based)
- **State Management**: TanStack Query (server state) + Zustand (client state)
- **Routing**: React Router v6
- **Forms**: React Hook Form + Zod
- **Testing**: Vitest, Testing Library

### Infrastructure
- **.NET Aspire**: Local development orchestration
- **Docker**: Containerization
- **PostgreSQL**: Primary data store
- **TimescaleDB**: Time-series extension for metrics
- **Valkey**: Distributed caching
- **NATS**: Message broker and event streaming
- **Azure Blob Storage**: Bundle artifact storage

## 📚 Documentation

### Getting Started
- [Project Overview](docs/project-overview.md) - High-level architecture and workflows
- [Running with Aspire](RUNNING_WITH_ASPIRE.md) - Local development with .NET Aspire
- [Docker Requirements](docs/DOCKER_REQUIREMENTS.md) - Docker setup guide

### Architecture
- [Domain Model](docs/architecture/domain-model.md) - DDD entities and aggregates
- [Technical Architecture](docs/architecture/technical-architecture.md) - Detailed system design
- [API Documentation](docs/architecture/api-overview.md) - REST API reference

### Features
- [Rollout Management](docs/features/rollouts.md) - Bundle deployment and tracking
- [Device Management](docs/features/devices.md) - Device lifecycle and grouping

### Development
- [Local Development Guide](docs/development/local-development.md) - Comprehensive setup guide
- [Contributing Guidelines](CONTRIBUTING.md) - How to contribute
- [Code Style](docs/development/code-style.md) - Coding conventions

### Operations
- [Deployment Guide](docs/operations/deployment.md) - Production deployment
- [Monitoring](docs/operations/monitoring.md) - Observability and metrics

## 🧪 Testing

### Run All Tests
```bash
dotnet test
```

### Run Unit Tests Only
```bash
dotnet test --filter Category!=Integration
```

### Run Integration Tests
```bash
# Requires Docker for Testcontainers
dotnet test --filter Category=Integration
```

### Frontend Tests
```bash
cd web
npm test
```

## 🔐 Security

### Authentication & Authorization
- **Devices**: API key authentication
- **Users**: JWT-based authentication (OIDC-ready)
- **API Gateway**: Rate limiting per tenant
- **Production**: Support for Azure AD, Zitadel, or any OIDC provider

### Best Practices
- API keys rotatable per device
- JWT tokens with short expiration
- HTTPS enforced in production
- Secrets managed via Azure Key Vault or environment variables

## 🤝 Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for:
- Development workflow
- Code review process
- Testing requirements
- Documentation standards

## 📄 License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Built with [.NET 9](https://dot.net) and [.NET Aspire](https://learn.microsoft.com/aspire)
- UI components from [shadcn/ui](https://ui.shadcn.com/)
- Inspired by the need for simple, effective edge device management

## 📞 Support

- **Documentation**: [docs/](docs/)
- **Issues**: [GitHub Issues](https://github.com/signalbeam-io/signalbeam-edge/issues)
- **Discussions**: [GitHub Discussions](https://github.com/signalbeam-io/signalbeam-edge/discussions)

---
