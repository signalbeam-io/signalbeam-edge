locals {
  location_short = "weu"
  # ACR names must be alphanumeric, no hyphens
  name = "${var.project}acr${var.environment}${local.location_short}"
}

resource "azurerm_container_registry" "this" {
  name                   = local.name
  resource_group_name    = var.resource_group_name
  location               = var.location
  sku                    = var.sku
  admin_enabled          = false
  anonymous_pull_enabled = false
  tags                   = var.tags

  # NOTE: Basic SKU does not support network rules (firewall, private endpoints).
  # Upgrade to Premium SKU for network isolation in production.
}
