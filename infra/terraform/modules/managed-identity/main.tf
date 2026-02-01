locals {
  location_short = "weu"
}

resource "azurerm_user_assigned_identity" "workload" {
  name                = "${var.project}-id-workload-${var.environment}-${local.location_short}"
  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = var.tags
}
