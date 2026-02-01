# SignalBeam Edge — Azure Infrastructure

Terraform + Terragrunt configuration for provisioning the SignalBeam Edge dev environment on Azure.

## Architecture

10 Terraform modules wired together by Terragrunt with the following dependency graph:

```
Phase 1: resource-group
Phase 2: networking, monitoring, container-registry, managed-identity, dns  (parallel)
Phase 3: key-vault, storage  (depend on networking + managed-identity)
Phase 4: postgresql  (depends on networking + key-vault)
Phase 5: aks-cluster  (depends on networking + ACR + monitoring + managed-identity + key-vault)
```

## Resources Provisioned

| Module | Resources | Est. Cost |
|--------|-----------|-----------|
| resource-group | Resource group `sb-rg-dev-weu` | — |
| networking | VNet, 3 subnets (AKS, PostgreSQL, Services), NSGs, private DNS zone | — |
| monitoring | Log Analytics workspace, ContainerInsights solution | $0 (5GB free) |
| container-registry | ACR Basic (`sbacrdevweu`) | ~$5/mo |
| managed-identity | User-assigned identity for workload identity | — |
| key-vault | Key Vault with RBAC, auto-generated PostgreSQL + Zitadel secrets | ~$1/mo |
| postgresql | Flexible Server B1ms, databases: `signalbeam`, `zitadel`, TimescaleDB | ~$13/mo |
| storage | Blob account + containers: `signalbeam-bundles`, `device-bundles` | ~$2/mo |
| dns | DNS zone `dev.signalbeam.io` | ~$0.50/mo |
| aks-cluster | AKS 1x B4ms, workload identity, Calico, Container Insights | ~$120/mo |
| **Total** | | **~$142/mo** |

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
│   └── aks-cluster/                     # AKS with workload identity
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
        └── aks-cluster/terragrunt.hcl
```
