locals {
  location_short = "weu"
}

# Azure Container Apps environment.
#
# Uses a workload-profiles environment with a single Consumption profile so that
# individual apps can scale to zero while the environment is VNet-injected via a
# /27 subnet (a Consumption-only environment would require a /23). The internal
# load balancer is disabled so that apps with external ingress (the ApiGateway)
# receive a public FQDN; apps with internal ingress stay private within the VNet.
resource "azurerm_container_app_environment" "this" {
  name                           = "${var.project}-cae-${var.environment}-${local.location_short}"
  location                       = var.location
  resource_group_name            = var.resource_group_name
  log_analytics_workspace_id     = var.log_analytics_workspace_id
  infrastructure_subnet_id       = var.infrastructure_subnet_id
  internal_load_balancer_enabled = var.internal_load_balancer_enabled

  workload_profile {
    name                  = "Consumption"
    workload_profile_type = "Consumption"
  }

  tags = var.tags
}

# --- Dedicated storage account for ACA Azure Files (NATS JetStream persistence) ---
#
# ACA mounts Azure Files via SMB, which requires the storage account key. The
# shared blob storage account has key auth disabled, so NATS gets its own
# account scoped to file shares only.
resource "azurerm_storage_account" "files" {
  name                            = "${var.project}staca${var.environment}${local.location_short}"
  resource_group_name             = var.resource_group_name
  location                        = var.location
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  account_kind                    = "StorageV2"
  min_tls_version                 = "TLS1_2"
  https_traffic_only_enabled      = true
  allow_nested_items_to_be_public = false
  shared_access_key_enabled       = true

  # SMB mounts require the account key, so lock the account to the ACA subnet
  # (via service endpoint) and deny public network access. AzureServices bypass
  # lets the ACA control plane register the share.
  network_rules {
    default_action             = "Deny"
    bypass                     = ["AzureServices"]
    virtual_network_subnet_ids = [var.aca_subnet_id]
  }

  tags = var.tags
}

resource "azurerm_storage_share" "nats" {
  name               = "nats-jetstream"
  storage_account_id = azurerm_storage_account.files.id
  quota              = var.nats_share_quota_gb
}

# Registers the file share with the environment so apps can mount it by name.
resource "azurerm_container_app_environment_storage" "nats" {
  name                         = "nats-jetstream"
  container_app_environment_id = azurerm_container_app_environment.this.id
  account_name                 = azurerm_storage_account.files.name
  share_name                   = azurerm_storage_share.nats.name
  access_key                   = azurerm_storage_account.files.primary_access_key
  access_mode                  = "ReadWrite"
}
