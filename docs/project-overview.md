# Project Overview: SignalBeam Edge

This document explains what SignalBeam Edge is, why it exists, and how the system is structured. It is derived from the business plan in `docs/signalbeam-edge-business-plan.md` and the current codebase layout.

## Why SignalBeam Edge

SignalBeam Edge is built for teams managing small-to-medium edge fleets (roughly 5–200 devices). These teams need a simple way to:

- Register devices without complex IoT infrastructure.
- See online/offline status and key health metrics.
- Roll out container updates to groups of devices with clear feedback.

The product intentionally avoids heavyweight cloud IoT concepts in the MVP and focuses on an opinionated, minimal model:

**device → group → bundle → status**

## Architecture overview

SignalBeam Edge is composed of a device-side agent, backend services, and a web UI.

### Services and responsibilities

- **Edge Agent** (`src/EdgeAgent/`)
  - Runs on the device (Linux ARM/x86).
  - Registers device identity and sends heartbeats.
  - Pulls desired bundle configuration and reconciles containers.
  - Reports status and deployment outcomes.

- **DeviceManager** (`src/DeviceManager/`)
  - Core API for devices, groups, tags, and desired state.
  - Stores device metadata, heartbeats, and activity history.
  - Exposes endpoints used by the agent and UI.

- **BundleOrchestrator** (`src/BundleOrchestrator/`)
  - Coordinates bundle assignments and rollout state.
  - Tracks deployment progress and outcomes across devices.

- **TelemetryProcessor** (`src/TelemetryProcessor/`)
  - Ingests and processes metrics/telemetry streams.

- **API Gateway** (`src/SignalBeam.ApiGateway/`)
  - Front door for web and external clients.
  - Aggregates API access into a single entry point.

- **Web UI** (`web/`)
  - React + TypeScript dashboard for fleet visibility.

### Infrastructure dependencies

Local development uses .NET Aspire to orchestrate:

- **PostgreSQL** for relational storage.
- **Valkey** (Redis-compatible) for caching.
- **NATS** for messaging/event streaming.

See `RUNNING_WITH_ASPIRE.md` for configuration details.

## Core workflows

### 1. Register device

1. Agent starts and sends registration data (tenant + device ID).
2. DeviceManager stores the device and marks it registered.
3. Device appears in the UI.

### 2. Heartbeat and metrics

1. Agent periodically sends status and metrics.
2. DeviceManager updates health state and last-seen.
3. UI shows online/offline and latest metrics snapshot.

### 3. Assign a bundle

1. User creates a bundle and version in the UI.
2. User assigns bundle to a device or group.
3. DeviceManager records desired state.

### 4. Reconcile on device

1. Agent fetches desired bundle.
2. Agent pulls images and starts/stops containers.
3. Agent reports deployment status and container state.

### 5. Rollout status

1. BundleOrchestrator aggregates device outcomes.
2. UI displays pending/in-progress/succeeded/failed counts.

## Local development guide

### Prerequisites

- .NET 9 SDK
- Docker Desktop
- Aspire workload

```bash
dotnet workload install aspire
```

### Run the full stack (recommended)

```bash
cd src/SignalBeam.AppHost
dotnet run
```

The Aspire dashboard opens at `http://localhost:15888` with logs, traces, and service endpoints.

### Run a single service

```bash
cd src/DeviceManager/SignalBeam.DeviceManager.Host
dotnet run
```

Note: PostgreSQL, Valkey, and NATS must be running separately.

### Frontend

```bash
cd web
npm install
npm run dev
```

### Build and test

```bash
dotnet restore
dotnet build
dotnet test
```

Integration tests require Docker and may need additional services.

## Related docs

- `docs/signalbeam-edge-business-plan.md`: market rationale and MVP scope.
- `docs/architecture/domain-model.md`: DDD model, entities, and events.
- `RUNNING_WITH_ASPIRE.md`: running the stack with Aspire.
- `docs/DOCKER_REQUIREMENTS.md`: Docker setup expectations.
