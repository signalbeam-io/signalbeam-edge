# API Gateway Helm Chart

Deploys the SignalBeam API Gateway — a YARP reverse proxy that routes requests to backend microservices.

## Quick Start

```bash
helm install api-gateway deploy/charts/api-gateway -n signalbeam --create-namespace

helm install api-gateway deploy/charts/api-gateway -n signalbeam \
  -f deploy/charts/api-gateway/values-dev.yaml
```

## Environment Values

| File | Replicas | HPA | Ingress |
|------|----------|-----|---------|
| `values.yaml` | 2 | 2-10 | off |
| `values-dev.yaml` | 1 | off | off |
| `values-staging.yaml` | 2 | 2-5 | on |
| `values-prod.yaml` | 3 | 3-10 | on + TLS |

## Backend Routing

YARP backend URLs are configured via `values.backends` and injected as environment variable overrides:

| Backend | Default URL |
|---------|------------|
| `deviceManager` | `http://device-manager.signalbeam` |
| `bundleOrchestrator` | `http://bundle-orchestrator.signalbeam` |
| `telemetryProcessor` | `http://telemetry-processor.signalbeam` |
| `identityManager` | `http://identity-manager.signalbeam` |
| `zitadel` | `http://zitadel.signalbeam:8080` |

## Notes

- This is the public entry point — Ingress should point here, not to individual services
- No database or secrets required (stateless proxy)
- Lower memory footprint than backend services (128Mi-512Mi)
- Production ingress terminates TLS via cert-manager

## Upgrade / Uninstall

```bash
helm upgrade api-gateway deploy/charts/api-gateway -n signalbeam \
  -f deploy/charts/api-gateway/values-prod.yaml

helm uninstall api-gateway -n signalbeam
```
