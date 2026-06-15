# SignalBeam Edge — Azure Infrastructure

Terraform + Terragrunt configuration for provisioning the SignalBeam Edge dev environment on Azure.

## Architecture

20 Terraform modules wired together by Terragrunt. There are **two deployment
paths** that share the same base (Phases 1–4):

**AKS path** (full stack, ~$142/mo):

```
Phase 1: resource-group
Phase 2: networking, monitoring, container-registry, managed-identity, dns  (parallel)
Phase 3: key-vault, storage  (depend on networking + managed-identity)
Phase 4: postgresql  (depends on networking + key-vault)
Phase 5: aks-cluster  (depends on networking + ACR + monitoring + managed-identity + key-vault)
Phase 6: cert-manager, ingress-nginx, nats, kube-prometheus-stack  (depend on aks-cluster)
Phase 7: loki, tempo  (depend on kube-prometheus-stack for monitoring namespace)
Phase 8: otel-collector  (depends on loki + tempo + kube-prometheus-stack)
```

**Azure Container Apps path** (lean/dogfood, ~$20/mo — replaces AKS phases 5–8):

```
Phase 1–4: (same base as above)
Phase 5: aca/environment  (depends on networking[aca subnet] + monitoring)
Phase 6: aca/secrets  (depends on key-vault + postgresql)
Phase 7: aca/nats, aca/apigateway, aca/devicemanager, aca/bundleorchestrator,
         aca/telemetryprocessor, aca/identitymanager  (depend on environment + secrets + managed-identity)
```

See [`terragrunt/dev/aca/README.md`](terragrunt/dev/aca/README.md) for the ACA runbook.

## Resources Provisioned

| Module | Resources | Est. Cost |
|--------|-----------|-----------|
| resource-group | Resource group `sb-rg-dev-weu` | — |
| networking | VNet, 4 subnets (AKS, PostgreSQL, Services, ACA), NSGs, private DNS zone | — |
| monitoring | Log Analytics workspace, ContainerInsights solution | $0 (5GB free) |
| container-registry | ACR Basic (`sbacrdevweu`) | ~$5/mo |
| managed-identity | User-assigned identity for workload identity | — |
| key-vault | Key Vault with RBAC, auto-generated PostgreSQL + Zitadel secrets | ~$1/mo |
| postgresql | Flexible Server B1ms, databases: `signalbeam`, `zitadel`, TimescaleDB | ~$13/mo |
| storage | Blob account + containers: `signalbeam-bundles`, `device-bundles` | ~$2/mo |
| dns | DNS zone `dev.signalbeam.io` | ~$0.50/mo |
| aks-cluster | AKS 1x B4ms, workload identity, Calico, Container Insights | ~$120/mo |
| nats | NATS 3-node cluster with JetStream (10Gi file storage), Prometheus exporter | ~$0 (runs on AKS) |
| cert-manager | TLS certificate management with Let's Encrypt ClusterIssuers | ~$0 (runs on AKS) |
| ingress-nginx | NGINX Ingress Controller (2 replicas) with Azure LoadBalancer | ~$0 (runs on AKS) |
| kube-prometheus-stack | Prometheus Operator, Prometheus (20Gi), Grafana (5Gi), AlertManager | ~$0 (runs on AKS) |
| loki | Log aggregation, single-binary mode (10Gi) | ~$0 (runs on AKS) |
| tempo | Distributed tracing backend (10Gi) | ~$0 (runs on AKS) |
| otel-collector | OpenTelemetry Collector (2 replicas), routes to Tempo/Prometheus/Loki | ~$0 (runs on AKS) |
| **Total (AKS path)** | | **~$142/mo** |

### Azure Container Apps path (lean/dogfood)

Replaces the AKS phases 5–8 with a Consumption-based Container Apps stack. Shares
the base modules (resource-group, networking, monitoring, managed-identity,
key-vault, postgresql). Valkey, Zitadel, and ACR are omitted.

| Module | Resources | Est. Cost |
|--------|-----------|-----------|
| container-app-environment | ACA env (Consumption, public LB) + storage account + Azure Files share for NATS | ~$1–3/mo (Files) |
| app-secrets | Key Vault secrets: DB connection string + GHCR PAT placeholder | — |
| container-app (×6) | ApiGateway (external) + DeviceManager, BundleOrchestrator, TelemetryProcessor, IdentityManager (internal, scale-to-zero), NATS (internal TCP, min 1) | NATS ~$5–7/mo; rest ~$0 (free grant) |
| postgresql (shared base) | Flexible Server B1ms | ~$13/mo |
| **Total (ACA path)** | | **~$20/mo** |

## Prerequisites

- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) installed
- [Terraform](https://developer.hashicorp.com/terraform/install) >= 1.5
- [Terragrunt](https://terragrunt.gruntwork.io/docs/getting-started/install/) installed
- [TFLint](https://github.com/terraform-linters/tflint) installed (optional, for linting)
- An Azure subscription with Owner or Contributor role

Install all tools via Homebrew:

```bash
brew install azure-cli terraform terragrunt tflint
```

## Deployment Runbook

### 1. Authenticate and set subscription (one-time)

```bash
az login
az account set --subscription "<YOUR_SUBSCRIPTION_ID>"
```

### 2. Set your subscription ID (one-time)

Edit [`infra/terragrunt/dev/env.hcl`](terragrunt/dev/env.hcl) and replace `YOUR_SUBSCRIPTION_ID_HERE` with your Azure subscription ID:

```bash
az account show --query id -o tsv
```

### 3. Bootstrap remote state storage (one-time)

```bash
chmod +x infra/scripts/bootstrap.sh
./infra/scripts/bootstrap.sh
```

This creates a separate resource group `sb-tfstate-dev-weu` with a storage account for Terraform state. See [`infra/scripts/bootstrap.sh`](scripts/bootstrap.sh).

### 4. Lint

```bash
# Format check across all modules
terraform fmt -check -recursive infra/terraform/modules

# Auto-fix formatting (if needed)
terraform fmt -recursive infra/terraform/modules

# Validate all modules via Terragrunt
cd infra/terragrunt/dev
terragrunt run --all validate

# TFLint per module (optional)
cd infra/terraform/modules
for dir in */; do (cd "$dir" && tflint); done
```

### 5. Plan

Preview all changes (respects the [dependency order](#architecture)):

```bash
cd infra/terragrunt/dev
terragrunt run --all plan
```

To plan a single module:

```bash
cd infra/terragrunt/dev/aks-cluster
terragrunt run plan
```

### 6. Apply

```bash
cd infra/terragrunt/dev
terragrunt run --all apply
```

Terragrunt resolves dependencies and applies modules in the correct order. To apply a single module:

```bash
cd infra/terragrunt/dev/aks-cluster
terragrunt run apply
```

### 7. Connect to the AKS cluster

```bash
az aks get-credentials --resource-group sb-rg-dev-weu --name sb-aks-dev-weu
kubectl get nodes
```

### 8. Configure DNS

Point your domain registrar's NS records for `dev.signalbeam.io` to the Azure DNS nameservers:

```bash
az network dns zone show -g sb-rg-dev-weu -n dev.signalbeam.io --query nameServers -o tsv
```

## Verification

```bash
# AKS cluster running
kubectl get nodes

# ACR pull works from AKS
az aks check-acr --name sb-aks-dev-weu --resource-group sb-rg-dev-weu --acr sbacrdevweu.azurecr.io

# PostgreSQL is private-only
az postgres flexible-server show --name sb-psql-dev-weu -g sb-rg-dev-weu --query "network.publicNetworkAccess"
# → "Disabled"

# Databases exist
az postgres flexible-server db list -g sb-rg-dev-weu --server-name sb-psql-dev-weu -o table

# Storage containers exist
az storage container list --account-name sbstdevweu --auth-mode login -o table

# Key Vault secrets stored
az keyvault secret list --vault-name sb-kv-dev-weu -o table

# DNS nameservers (point registrar NS records here)
az network dns zone show -g sb-rg-dev-weu -n dev.signalbeam.io --query nameServers -o tsv

# NATS cluster healthy
kubectl -n signalbeam get pods -l app.kubernetes.io/name=nats
kubectl -n signalbeam exec -it nats-0 -- nats server check jetstream

# JetStream streams created
kubectl -n signalbeam exec -it nats-0 -- nats stream ls

# Ingress controller running with external IP
kubectl -n ingress-nginx get svc ingress-nginx-controller

# cert-manager ready
kubectl -n cert-manager get pods
kubectl get clusterissuer

# Prometheus, Grafana, AlertManager running
kubectl -n monitoring get pods

# Loki and Tempo running
kubectl -n monitoring get pods -l app.kubernetes.io/name=loki
kubectl -n monitoring get pods -l app.kubernetes.io/name=tempo

# OTEL Collector running
kubectl -n signalbeam get pods -l app.kubernetes.io/name=otel-collector
```

## Ongoing Changes

For day-to-day work after initial setup, the cycle is:

```bash
# 1. Edit modules
# 2. Lint
terraform fmt -check -recursive infra/terraform/modules
cd infra/terragrunt/dev && terragrunt run --all validate

# 3. Plan
cd infra/terragrunt/dev && terragrunt run --all plan

# 4. Apply
cd infra/terragrunt/dev && terragrunt run --all apply
```

## Destroy

To tear down all infrastructure:

```bash
cd infra/terragrunt/dev
terragrunt run --all destroy
```

## Security Notes

- **No public PostgreSQL access** — VNet-integrated via delegated subnet
- **Storage firewall** — deny by default, allow AKS subnet only
- **Key Vault RBAC** — least-privilege roles, no access policies
- **Workload identity** — no secrets in pods, Azure RBAC for blob/KV/DNS
- **ACR admin disabled** — AKS pulls via managed identity AcrPull role
- **NSGs on all subnets** — PostgreSQL only reachable from AKS subnet
- **TLS 1.2 minimum** on storage and Key Vault
- **Calico network policies** in AKS for namespace isolation
- **Auto-generated secrets** — PostgreSQL password + Zitadel key stored in Key Vault

**ACA path additionally:**
- **Delegated ACA subnet** (`snet-aca`, /27) with `Microsoft.App/environments` delegation; Postgres NSG explicitly allows it on 5432
- **Key Vault references** — container apps read secrets via the managed identity (`Key Vault Secrets User`); values never enter the app definitions
- **NATS Azure Files account** denies public access, restricted to the ACA subnet via a `Microsoft.Storage` service endpoint
- **Private GHCR** — pull token held in Key Vault (`ghcr-pat`), set out-of-band so it never enters Terraform state
- See [`terragrunt/dev/aca/README.md`](terragrunt/dev/aca/README.md#terraform-state-contains-sensitive-material) for state-handling guidance

## File Structure

```
infra/
├── scripts/
│   └── bootstrap.sh                    # Creates Azure Storage for tfstate
├── terraform/modules/
│   ├── resource-group/                  # variables.tf, main.tf, outputs.tf
│   ├── networking/                      # VNet, subnets, NSGs, private DNS
│   ├── monitoring/                      # Log Analytics + ContainerInsights
│   ├── container-registry/              # ACR Basic
│   ├── managed-identity/                # User-assigned identity
│   ├── key-vault/                       # KV with RBAC + auto-generated secrets
│   ├── postgresql/                      # Flexible Server + databases
│   ├── storage/                         # Blob account + containers
│   ├── dns/                             # Azure DNS zone
│   ├── aks-cluster/                     # AKS with workload identity
│   ├── nats/                            # NATS cluster with JetStream (Helm)
│   ├── cert-manager/                    # TLS cert management + Let's Encrypt
│   ├── ingress-nginx/                   # NGINX Ingress Controller
│   ├── kube-prometheus-stack/           # Prometheus + Grafana + AlertManager
│   ├── loki/                            # Log aggregation
│   ├── tempo/                           # Distributed tracing
│   ├── otel-collector/                  # OpenTelemetry Collector
│   ├── container-app-environment/       # ACA env + Azure Files for NATS (ACA path)
│   ├── container-app/                   # Reusable Container App (ACA path)
│   └── app-secrets/                     # KV secrets for ACA (ACA path)
└── terragrunt/
    ├── terragrunt.hcl                   # Root config: backend, provider, inputs
    └── dev/
        ├── env.hcl                      # Dev environment variables
        ├── resource-group/terragrunt.hcl
        ├── networking/terragrunt.hcl
        ├── monitoring/terragrunt.hcl
        ├── container-registry/terragrunt.hcl
        ├── managed-identity/terragrunt.hcl
        ├── key-vault/terragrunt.hcl
        ├── postgresql/terragrunt.hcl
        ├── storage/terragrunt.hcl
        ├── dns/terragrunt.hcl
        ├── aks-cluster/terragrunt.hcl
        ├── nats/terragrunt.hcl
        ├── cert-manager/terragrunt.hcl
        ├── ingress-nginx/terragrunt.hcl
        ├── kube-prometheus-stack/terragrunt.hcl
        ├── loki/terragrunt.hcl
        ├── tempo/terragrunt.hcl
        ├── otel-collector/terragrunt.hcl
        └── aca/                         # Container Apps path (alternative to AKS)
            ├── environment/terragrunt.hcl
            ├── secrets/terragrunt.hcl
            ├── nats/terragrunt.hcl
            ├── apigateway/terragrunt.hcl
            ├── devicemanager/terragrunt.hcl
            ├── bundleorchestrator/terragrunt.hcl
            ├── telemetryprocessor/terragrunt.hcl
            └── identitymanager/terragrunt.hcl
```

## NATS Architecture

NATS is deployed as a 3-node cluster with JetStream enabled for persistent messaging. It serves as the message broker for all inter-service communication.

### Deployment

- **Helm chart:** `nats/nats` from `nats-io.github.io/k8s/helm/charts/`
- **Replicas:** 3 (clustered for HA)
- **JetStream storage:** 10Gi file-based per node
- **Monitoring:** HTTP monitor on port 8222, Prometheus exporter with PodMonitor
- **Service endpoint:** `nats://nats.signalbeam:4222`

### JetStream Streams

| Stream | Subjects | Retention | Max Age | Replicas |
|--------|----------|-----------|---------|----------|
| DEVICE_EVENTS | `signalbeam.devices.events.>` | Limits | 30d | 3 |
| BUNDLE_ASSIGNMENTS | `signalbeam.bundles.assignments.>`, `signalbeam.bundles.rollouts.>` | Limits | 7d | 3 |
| TELEMETRY_METRICS | `signalbeam.telemetry.metrics.>` | Limits | 3d | 3 |
| DEVICE_STATUS | `signalbeam.devices.status.>` | Limits | 7d | 3 |

### Subject Hierarchy

```
signalbeam.devices.heartbeat.<deviceId>      # Core NATS (ephemeral, no stream)
signalbeam.devices.events.<eventType>        # → DEVICE_EVENTS stream
signalbeam.devices.commands.<deviceId>       # Core NATS request/reply
signalbeam.devices.status.<deviceId>         # → DEVICE_STATUS stream
signalbeam.bundles.assignments.<deviceId>    # → BUNDLE_ASSIGNMENTS stream
signalbeam.bundles.rollouts.<rolloutId>      # → BUNDLE_ASSIGNMENTS stream
signalbeam.telemetry.metrics.<deviceId>      # → TELEMETRY_METRICS stream
```

Heartbeats and commands use Core NATS (ephemeral pub/sub, request/reply). All other subjects are persisted in JetStream streams for reliable delivery and replay.

## Ingress & TLS

- **Ingress Controller:** NGINX (`ingress-nginx`) with Azure LoadBalancer, 2 replicas
- **TLS:** cert-manager with Let's Encrypt ClusterIssuers (`letsencrypt-prod`, `letsencrypt-staging`)
- **DNS01 Validation:** Azure DNS via workload identity (federated credentials provisioned in AKS module)
- **IngressClass:** `nginx` (set as default)

All production Helm values reference `ingressClassName: nginx` and `cert-manager.io/cluster-issuer: letsencrypt-prod`.

## Observability Stack

The observability pipeline collects traces, metrics, and logs from all microservices:

```
Microservices → OTEL Collector → Tempo (traces)
                               → Prometheus (metrics)
                               → Loki (logs)
                               → Grafana (dashboards)
```

### Components

| Component | Namespace | Endpoint | Purpose |
|-----------|-----------|----------|---------|
| OTEL Collector | `signalbeam` | `otel-collector.signalbeam:4317` (gRPC) | Receives OTLP telemetry, routes to backends |
| Prometheus | `monitoring` | `kube-prometheus-stack-prometheus:9090` | Metrics storage, ServiceMonitor scraping |
| Grafana | `monitoring` | `kube-prometheus-stack-grafana:80` | Dashboards (Loki + Tempo + Prometheus datasources pre-configured) |
| Loki | `monitoring` | `loki.monitoring:3100` | Log aggregation (single-binary, 7d retention) |
| Tempo | `monitoring` | `tempo.monitoring:3100` | Distributed tracing (7d retention) |
| AlertManager | `monitoring` | `kube-prometheus-stack-alertmanager:9093` | Alert routing |

### Data Flow

- **Traces:** Microservices → OTLP gRPC → OTEL Collector → Tempo
- **Metrics:** OTEL Collector → Prometheus remote write; Prometheus also scrapes ServiceMonitors directly
- **Logs:** Microservices → OTLP gRPC → OTEL Collector → Loki

All microservice Helm charts define ServiceMonitors that Prometheus Operator auto-discovers.
