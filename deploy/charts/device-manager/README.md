# DeviceManager Helm Chart

Deploys the SignalBeam DeviceManager service — device registration, state management, and monitoring.

## Quick Start

```bash
# Install with default values
helm install device-manager deploy/charts/device-manager -n signalbeam --create-namespace

# Install with environment-specific values
helm install device-manager deploy/charts/device-manager -n signalbeam \
  -f deploy/charts/device-manager/values-dev.yaml
```

## Environment Values

| File | Replicas | HPA | Workload Identity | Ingress |
|------|----------|-----|-------------------|---------|
| `values.yaml` | 2 | 2-10 | off | off |
| `values-dev.yaml` | 1 | off | off | off |
| `values-staging.yaml` | 2 | 2-5 | on | off |
| `values-prod.yaml` | 3 | 3-10 | on | on |

## Prerequisites

Create Kubernetes secrets before installing:

```bash
kubectl create secret generic device-manager-db -n signalbeam \
  --from-literal=connection-string="Host=postgres;Database=devicemanager;Username=app;Password=secret"

kubectl create secret generic device-manager-valkey -n signalbeam \
  --from-literal=connection-string="valkey:6379"
```

## Configuration

Key values in `values.yaml`:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `image.repository` | `ghcr.io/signalbeam-io/device-manager` | Container image |
| `image.tag` | `appVersion` | Image tag |
| `service.port` | `80` | Service port |
| `service.targetPort` | `8080` | Container port |
| `autoscaling.minReplicas` | `2` | Min HPA replicas |
| `autoscaling.maxReplicas` | `10` | Max HPA replicas |
| `workloadIdentity.enabled` | `false` | Azure Workload Identity |
| `serviceMonitor.enabled` | `true` | Prometheus ServiceMonitor |
| `config.*` | — | Environment variables via ConfigMap |
| `secrets` | — | Secret references for sensitive config |

## Upgrade

```bash
helm upgrade device-manager deploy/charts/device-manager -n signalbeam \
  -f deploy/charts/device-manager/values-prod.yaml
```

## Uninstall

```bash
helm uninstall device-manager -n signalbeam
```
