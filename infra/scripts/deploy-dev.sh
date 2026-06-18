#!/usr/bin/env bash
set -euo pipefail

# Provision the SignalBeam Edge dev infrastructure on Azure via Terragrunt.
# Applies the base platform first, then the Azure Container Apps (ACA) stack.
# This is the scripted equivalent of the steps in infra/terragrunt/dev/aca/README.md.
#
# Run infra/scripts/bootstrap.sh once first to create the Terraform state backend.
# App images are deployed separately (build + `az containerapp update`, see deploy.yml).
#
# Prerequisites:
#   - Azure CLI logged in (`az login`) on the correct subscription
#   - subscription_id set in infra/terragrunt/dev/env.hcl
#   - Terragrunt + Terraform installed
#
# Usage:
#   ./infra/scripts/deploy-dev.sh [plan|apply]   # default: plan (read-only, safe)

ACTION="${1:-plan}"

if [[ "${ACTION}" != "plan" && "${ACTION}" != "apply" ]]; then
  echo "Usage: $0 [plan|apply]" >&2
  exit 2
fi

# Resolve paths relative to this script so it runs from anywhere.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEV_DIR="${SCRIPT_DIR}/../terragrunt/dev"

# Base Azure units the ACA stack depends on (resolved in dependency order by Terragrunt).
BASE_DIRS=(resource-group networking managed-identity monitoring key-vault postgresql)

echo "==> Preflight checks"

if ! az account show >/dev/null 2>&1; then
  echo "Not logged in to Azure. Run: az login" >&2
  exit 1
fi

SUB_NAME=$(az account show --query name -o tsv)
SUB_ID=$(az account show --query id -o tsv)
echo "    Subscription: ${SUB_NAME} (${SUB_ID})"

if grep -q "YOUR_SUBSCRIPTION_ID_HERE" "${DEV_DIR}/env.hcl"; then
  echo "subscription_id is still a placeholder in ${DEV_DIR}/env.hcl — set it first." >&2
  exit 1
fi

# The Key Vault denies public access and only allows its subnets; data-plane
# secret reads/writes run from this host, so it must be allowlisted via the vault
# firewall. The module ignores ip_rules (operator/CI IPs are ephemeral, not infra
# state), so we add the rule out-of-band with `az` rather than via a Terraform var.
# Auto-detect the public IP unless KV_ALLOWED_IPS is preset.
if [[ -z "${KV_ALLOWED_IPS:-}" ]]; then
  KV_ALLOWED_IPS="$(curl -fsS --max-time 10 https://api.ipify.org || true)"
  if [[ -z "${KV_ALLOWED_IPS}" ]]; then
    echo "Could not auto-detect public IP for the Key Vault firewall." >&2
    echo "Set it manually, e.g.: KV_ALLOWED_IPS=\$(curl -s https://api.ipify.org) $0 ${ACTION}" >&2
    exit 1
  fi
fi
# Pass it as the create-time default too (used only when the vault doesn't exist yet).
export KV_ALLOWED_IPS
KV_NAME="sb-kv-dev-neu"
if [[ "${ACTION}" == "apply" ]] && az keyvault show --name "${KV_NAME}" >/dev/null 2>&1; then
  echo "    Allowlisting ${KV_ALLOWED_IPS} on ${KV_NAME} (out-of-band, az)…"
  az keyvault network-rule add --name "${KV_NAME}" --ip-address "${KV_ALLOWED_IPS}" --output none
  echo "    Tip: remove it after provisioning — az keyvault network-rule remove --name ${KV_NAME} --ip-address ${KV_ALLOWED_IPS}"
fi

# Build the --queue-include-dir flags for the base platform run.
BASE_INCLUDES=()
for d in "${BASE_DIRS[@]}"; do
  BASE_INCLUDES+=(--queue-include-dir "./${d}")
done

cd "${DEV_DIR}"

echo ""
echo "==> [1/2] Base platform (${BASE_DIRS[*]}) — ${ACTION}"
terragrunt run --all "${ACTION}" "${BASE_INCLUDES[@]}"

echo ""
echo "==> [2/2] ACA stack — ${ACTION}"
terragrunt run --all "${ACTION}" --working-dir ./aca

echo ""
if [[ "${ACTION}" == "apply" ]]; then
  echo "Infrastructure applied. Next steps:"
  echo "  1. Set the GHCR PAT (read:packages) so apps can pull private images:"
  echo "       az keyvault secret set --vault-name sb-kv-dev-weu --name ghcr-pat --value <YOUR_GHCR_PAT>"
  echo "  2. Run EF Core migrations against the (VNet-private) Postgres."
  echo "  3. Deploy app images (push to GHCR + az containerapp update, or trigger deploy.yml)."
  echo "  4. Get the agent's --cloud-url:"
  echo "       terragrunt output --working-dir ./aca/apigateway fqdn"
else
  echo "Plan complete. Re-run with 'apply' to create resources:"
  echo "  ./infra/scripts/deploy-dev.sh apply"
fi
