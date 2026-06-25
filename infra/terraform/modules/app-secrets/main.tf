# Application secrets stored in Key Vault for the ACA control plane.
#
# These are consumed by the container apps as Key Vault *references* (resolved at
# runtime by the workload managed identity), so the values are never embedded in
# the container app resource definitions.

# Postgres connection string for the shared "signalbeam" database. Azure Flexible
# Server enforces TLS (require_secure_transport=on); Trust Server Certificate is
# acceptable for in-VNet dogfood traffic.
resource "azurerm_key_vault_secret" "db_connection" {
  name = "db-connection-signalbeam"
  value = format(
    "Host=%s;Port=5432;Database=%s;Username=%s;Password=%s;Ssl Mode=Require;Trust Server Certificate=true",
    var.postgres_fqdn,
    var.database_name,
    var.administrator_login,
    var.administrator_password,
  )
  key_vault_id = var.key_vault_id
  tags         = var.tags
}

# GHCR personal access token (read:packages). Created as a placeholder here so the
# secret and ACA Key Vault reference can be provisioned; the real PAT is set
# out-of-band so it never enters Terraform state:
#   az keyvault secret set --vault-name <kv> --name ghcr-pat --value <PAT>
resource "azurerm_key_vault_secret" "ghcr_pat" {
  name         = "ghcr-pat"
  value        = "REPLACE_VIA_AZ_CLI"
  key_vault_id = var.key_vault_id
  tags         = var.tags

  lifecycle {
    ignore_changes = [value]
  }
}

# Tenant API key for the MVP/dogfood auth path (X-Api-Key header). Generated
# strong here and injected into the API-key services (DeviceManager,
# BundleOrchestrator, IdentityManager) as a Key Vault reference, so the deployed
# apps never rely on the guessable `dev-api-key-1` placeholders baked into
# appsettings.json (those remain for LOCAL dev only). The env injection overrides
# `Authentication:ApiKeys:0` at runtime, so the weak keys are invalid in the cloud
# even on already-built images.
#
# Alphanumeric only (special = false): the key travels in an HTTP header and the
# validator splits the composite on ':', so the key itself must contain no ':'.
resource "random_password" "tenant_api_key" {
  length  = 48
  special = false
}

locals {
  # Server-side tenant id the validator returns for this key. Kept as the existing
  # dev tenant id so tenant-scoped data created during the dogfood stays addressable.
  tenant_id = "00000000-0000-0000-0000-000000000001"
  # All scopes across services in one key — each service only checks the scope its
  # endpoint needs, so a superset is harmless and lets one dashboard key work
  # everywhere (the same way the old dev-api-key-1 did).
  tenant_api_scopes = "devices:read,devices:write,bundles:read,bundles:write,identity:read,identity:write"
}

# Composite `tenantId:key:scopes` — the exact string the ApiKeyValidator parses.
# Referenced as `Authentication__ApiKeys__0` by the service apps.
resource "azurerm_key_vault_secret" "tenant_api_key" {
  name         = "tenant-api-key"
  value        = "${local.tenant_id}:${random_password.tenant_api_key.result}:${local.tenant_api_scopes}"
  key_vault_id = var.key_vault_id
  tags         = var.tags
}

# The raw key value on its own — what a human pastes into the dashboard's API-key
# login. (The composite above is for the services; this is for people.)
resource "azurerm_key_vault_secret" "tenant_api_key_value" {
  name         = "tenant-api-key-value"
  value        = random_password.tenant_api_key.result
  key_vault_id = var.key_vault_id
  tags         = var.tags
}

# Zitadel first-instance admin password. Generated here (not out-of-band) because
# it only ever seeds the initial admin login at bootstrap; rotate via the Zitadel
# console afterwards. Complexity satisfies Zitadel's default password policy
# (upper/lower/number/symbol). override_special avoids characters that are awkward
# to paste from `terragrunt output`.
resource "random_password" "zitadel_admin" {
  length           = 24
  min_upper        = 2
  min_lower        = 2
  min_numeric      = 2
  min_special      = 2
  override_special = "!#%*-_=+"
}

resource "azurerm_key_vault_secret" "zitadel_admin_password" {
  name         = "zitadel-admin-password"
  value        = random_password.zitadel_admin.result
  key_vault_id = var.key_vault_id
  tags         = var.tags

  # Only ever seeds the first-instance admin. Once the operator rotates it in the
  # Zitadel console, don't let a later apply overwrite it with a fresh random
  # value (same guard as the GHCR PAT above).
  lifecycle {
    ignore_changes = [value]
  }
}
