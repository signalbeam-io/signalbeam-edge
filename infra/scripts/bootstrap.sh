#!/usr/bin/env bash
set -euo pipefail

# Bootstrap script for SignalBeam Edge Terraform state backend.
# Creates an Azure Storage Account for Terraform remote state.
# Run once before the first `terragrunt run-all apply`.
#
# Prerequisites:
#   - Azure CLI installed and logged in (`az login`)
#   - Correct subscription selected (`az account set --subscription <id>`)
#
# Usage:
#   chmod +x infra/scripts/bootstrap.sh
#   ./infra/scripts/bootstrap.sh

ENVIRONMENT="${1:-dev}"
LOCATION="${2:-westeurope}"
PROJECT="sb"

RG_NAME="${PROJECT}-tfstate-${ENVIRONMENT}-weu"
SA_NAME="${PROJECT}tfstate${ENVIRONMENT}weu"
CONTAINER_NAME="tfstate"

echo "==> Creating resource group: ${RG_NAME}"
az group create \
  --name "${RG_NAME}" \
  --location "${LOCATION}" \
  --tags environment="${ENVIRONMENT}" project=signalbeam managed-by=bootstrap

echo "==> Creating storage account: ${SA_NAME}"
az storage account create \
  --name "${SA_NAME}" \
  --resource-group "${RG_NAME}" \
  --location "${LOCATION}" \
  --sku Standard_LRS \
  --kind StorageV2 \
  --min-tls-version TLS1_2 \
  --allow-blob-public-access false \
  --tags environment="${ENVIRONMENT}" project=signalbeam managed-by=bootstrap

echo "==> Creating blob container: ${CONTAINER_NAME}"
az storage container create \
  --name "${CONTAINER_NAME}" \
  --account-name "${SA_NAME}" \
  --auth-mode login

echo "==> Enabling blob versioning"
az storage account blob-service-properties update \
  --account-name "${SA_NAME}" \
  --resource-group "${RG_NAME}" \
  --enable-versioning true

echo ""
echo "Bootstrap complete."
echo "  Resource Group:   ${RG_NAME}"
echo "  Storage Account:  ${SA_NAME}"
echo "  Container:        ${CONTAINER_NAME}"
echo ""
echo "You can now run: cd infra/terragrunt/dev && terragrunt run-all apply"
