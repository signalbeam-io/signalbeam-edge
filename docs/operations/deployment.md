# Deployment Guide

How to deploy the SignalBeam control plane to Azure. Two paths share the same base
infrastructure and diverge at the compute layer.

| Path | Compute | Cost | Use when |
|------|---------|------|----------|
| **AKS** | Kubernetes + Helm | ~$142/mo | Multi-replica HA, in-cluster observability, many devices |
| **Azure Container Apps** | ACA Consumption | ~$20/mo | Single dogfood device, cost-sensitive, scale-to-zero |

Both provision from `infra/`. Detailed runbooks: [`infra/README.md`](../../infra/README.md)
(AKS) and [`infra/terragrunt/dev/aca/README.md`](../../infra/terragrunt/dev/aca/README.md) (ACA).

## Prerequisites

```bash
brew install azure-cli terraform terragrunt
az login
az account set --subscription "<SUBSCRIPTION_ID>"
```

- Azure subscription with Owner or Contributor.
- Set the subscription ID in `infra/terragrunt/dev/env.hcl`.
- Bootstrap the remote state backend once: `./infra/scripts/bootstrap.sh`. This
  creates `sb-tfstate-dev-weu` and a state storage account.

## Base infrastructure (both paths)

Phases 1–4 are identical: resource group, networking, monitoring, managed
identity, Key Vault, Postgres, storage.

```bash
cd infra/terragrunt/dev
terragrunt run --all apply \
  --queue-include-dir ./resource-group --queue-include-dir ./networking \
  --queue-include-dir ./monitoring --queue-include-dir ./managed-identity \
  --queue-include-dir ./key-vault --queue-include-dir ./postgresql --queue-include-dir ./storage
```

Terragrunt resolves the dependency order. Postgres is private (VNet-only); its
admin password is generated into Key Vault.

## Path A — AKS

```bash
cd infra/terragrunt/dev
terragrunt run --all apply          # provisions AKS + ingress + NATS + observability
az aks get-credentials --resource-group sb-rg-dev-weu --name sb-aks-dev-weu

# One chart per service under deploy/charts/
for c in device-manager bundle-orchestrator telemetry-processor identity-manager api-gateway web; do
  helm upgrade --install "$c" "deploy/charts/$c" -n signalbeam --create-namespace
done
kubectl get pods -n signalbeam
```

The public entry point is the NGINX ingress LoadBalancer IP; TLS is issued by
cert-manager. See [`infra/README.md`](../../infra/README.md) for DNS, ingress, and
the observability stack.

## Path B — Azure Container Apps

```bash
cd infra/terragrunt/dev
terragrunt run --all apply --working-dir ./aca   # environment, secrets, NATS, gateway + 4 services
```

Set the GHCR pull token out-of-band (it never enters Terraform state), then read
the public gateway URL for the agent's `--cloud-url`:

```bash
az keyvault secret set --vault-name sb-kv-dev-weu --name ghcr-pat --value <PAT>
terragrunt output --working-dir ./aca/apigateway fqdn
```

Images come from `ghcr.io/signalbeam-io/{service}:latest`; publish them before
applying. The ACA path omits Valkey and Zitadel and routes logs to Log Analytics.

### Continuous deployment (GitHub Actions)

Once the Container Apps exist, `.github/workflows/deploy.yml` handles redeploys on
every push to `main` (or via `workflow_dispatch`). For each service it builds and
pushes the image to GHCR, runs `az containerapp update` to the new `${GITHUB_SHA::7}`
tag, waits for the revision to reach `Running`, then smoke-tests the gateway's
`/health/live`. It authenticates to Azure with OIDC — no stored credentials.

One-time setup:

- Create an Azure AD app with a federated credential for this repo/branch and set
  the `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` repo secrets.
- Grant the deployer service principal **Contributor** on each `sb-ca-*-dev` app so
  it can swap the image tag.
- The apps already hold the `ghcr-pat` registry secret (set above), so they can pull
  the private images.

## Database migrations

Apply EF Core migrations against the deployed Postgres. The server is private —
run from a VNet-connected host or temporarily allow your IP. The connection string
is in Key Vault as `db-connection-signalbeam`.

```bash
dotnet ef database update --project src/DeviceManager/SignalBeam.DeviceManager.Infrastructure
# repeat per service that owns schema
```

## Verify

```bash
# Postgres is private
az postgres flexible-server show -g sb-rg-dev-weu -n sb-psql-dev-weu \
  --query "network.publicNetworkAccess"          # → Disabled

# Public registration endpoint reachable (substitute the gateway host)
curl -i https://<gateway-fqdn>/api/devices
```

AKS adds `kubectl get pods -n signalbeam` and the ingress external IP. ACA exposes
health at `/health/live` and `/health/ready` per app.

## Teardown and cost control

```bash
# ACA: destroy compute between dogfood sessions (drops to ~$3–5/mo storage)
terragrunt run --all destroy --working-dir ./aca

# Full teardown
cd infra/terragrunt/dev && terragrunt run --all destroy
```

Postgres can also be stopped overnight to halve its cost. Destroying the ACA stack
removes the NATS JetStream Azure Files share — acceptable for a dogfood, where
message data is transient.
