# BundleOrchestrator Helm Chart

Deploys the SignalBeam BundleOrchestrator service — bundle management, versioning, and rollout orchestration with Azure Blob Storage integration.

## Quick Start

```bash
helm install bundle-orchestrator deploy/charts/bundle-orchestrator -n signalbeam --create-namespace

# With environment-specific values
helm install bundle-orchestrator deploy/charts/bundle-orchestrator -n signalbeam \
  -f deploy/charts/bundle-orchestrator/values-dev.yaml
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
kubectl create secret generic bundle-orchestrator-db -n signalbeam \
  --from-literal=connection-string="Host=postgres;Database=signalbeam_bundles;Username=app;Password=secret"

kubectl create secret generic bundle-orchestrator-blob -n signalbeam \
  --from-literal=service-uri="https://signalbeam.blob.core.windows.net"
```

### Azure Workload Identity (Staging/Production)

The BundleOrchestrator uses Azure Workload Identity to access Blob Storage without storing credentials. Set `workloadIdentity.enabled=true` and provide the managed identity `clientId`:

```bash
helm install bundle-orchestrator deploy/charts/bundle-orchestrator -n signalbeam \
  -f deploy/charts/bundle-orchestrator/values-prod.yaml \
  --set workloadIdentity.clientId=<your-managed-identity-client-id>
```

The ServiceAccount will be annotated with `azure.workload.identity/client-id` and pods labeled with `azure.workload.identity/use: "true"`.

## Configuration

| Parameter | Default | Description |
|-----------|---------|-------------|
| `image.repository` | `ghcr.io/signalbeam-io/bundle-orchestrator` | Container image |
| `image.tag` | `appVersion` | Image tag |
| `service.port` | `80` | Service port |
| `service.targetPort` | `8080` | Container port |
| `autoscaling.minReplicas` | `2` | Min HPA replicas |
| `autoscaling.maxReplicas` | `10` | Max HPA replicas |
| `workloadIdentity.enabled` | `false` | Azure Workload Identity |
| `workloadIdentity.clientId` | `""` | Managed identity client ID |
| `config.AzureBlobStorage__ContainerName` | `signalbeam-bundles` | Blob container name |
| `serviceMonitor.enabled` | `true` | Prometheus ServiceMonitor |

## Upgrade / Uninstall

```bash
helm upgrade bundle-orchestrator deploy/charts/bundle-orchestrator -n signalbeam \
  -f deploy/charts/bundle-orchestrator/values-prod.yaml

helm uninstall bundle-orchestrator -n signalbeam
```
