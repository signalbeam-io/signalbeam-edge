locals {
  location_short = lookup({ westeurope = "weu", northeurope = "neu" }, var.location, "weu")
}

data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "this" {
  name                       = "${var.project}-kv-${var.environment}-${local.location_short}"
  location                   = var.location
  resource_group_name        = var.resource_group_name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  soft_delete_retention_days = var.soft_delete_retention_days
  purge_protection_enabled   = false # dev only — set true for prod
  enable_rbac_authorization  = true

  network_acls {
    default_action             = "Deny"
    bypass                     = "AzureServices"
    virtual_network_subnet_ids = compact([var.aks_subnet_id, var.services_subnet_id, var.aca_subnet_id])
    ip_rules                   = var.allowed_ip_addresses
  }

  # Operator/CI IPs are ephemeral and managed out-of-band (e.g. `az keyvault
  # network-rule add` during bootstrap, removed afterwards). Terraform sets the
  # initial ip_rules from var.allowed_ip_addresses on create, but must not strip
  # rules added later — otherwise every apply that touches this unit (including
  # as a dependency of the ACA stack) locks the operator out of the data plane.
  lifecycle {
    ignore_changes = [network_acls[0].ip_rules]
  }

  tags = var.tags
}

# Current user gets Key Vault Administrator
resource "azurerm_role_assignment" "kv_admin" {
  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Administrator"
  principal_id         = data.azurerm_client_config.current.object_id
}

# Workload identity gets Key Vault Secrets User
resource "azurerm_role_assignment" "kv_secrets_user" {
  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = var.workload_identity_principal_id
}

# --- Auto-generated secrets ---

resource "random_password" "postgresql_admin" {
  length  = 32
  special = true
}

resource "random_password" "zitadel_master_key" {
  length  = 32
  special = true
}

resource "azurerm_key_vault_secret" "postgresql_admin_password" {
  name         = "postgresql-admin-password"
  value        = random_password.postgresql_admin.result
  key_vault_id = azurerm_key_vault.this.id

  depends_on = [azurerm_role_assignment.kv_admin]
}

resource "azurerm_key_vault_secret" "zitadel_master_key" {
  name         = "zitadel-master-key"
  value        = random_password.zitadel_master_key.result
  key_vault_id = azurerm_key_vault.this.id

  depends_on = [azurerm_role_assignment.kv_admin]
}
