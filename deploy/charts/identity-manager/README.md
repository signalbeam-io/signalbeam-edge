# IdentityManager Helm Chart

Deploys the SignalBeam IdentityManager service — user authentication, tenant management, team invitations, and subscriptions.

## Quick Start

```bash
helm install identity-manager deploy/charts/identity-manager -n signalbeam --create-namespace

helm install identity-manager deploy/charts/identity-manager -n signalbeam \
  -f deploy/charts/identity-manager/values-dev.yaml
```

## Environment Values

| File | Replicas | HPA |
|------|----------|-----|
| `values.yaml` | 2 | 2-8 |
| `values-dev.yaml` | 1 | off |
| `values-staging.yaml` | 2 | 2-4 |
| `values-prod.yaml` | 2 | 2-8 |

## Prerequisites

```bash
kubectl create secret generic identity-manager-db -n signalbeam \
  --from-literal=connection-string="Host=postgres;Database=signalbeam_identity;Username=app;Password=secret"

kubectl create secret generic identity-manager-auth -n signalbeam \
  --from-literal=authority="https://auth.signalbeam.io"
```

## Upgrade / Uninstall

```bash
helm upgrade identity-manager deploy/charts/identity-manager -n signalbeam \
  -f deploy/charts/identity-manager/values-prod.yaml

helm uninstall identity-manager -n signalbeam
```
