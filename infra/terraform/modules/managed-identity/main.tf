locals {
  location_short = lookup({ westeurope = "weu", northeurope = "neu" }, var.location, "weu")
}

resource "azurerm_user_assigned_identity" "workload" {
  name                = "${var.project}-id-workload-${var.environment}-${local.location_short}"
  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = var.tags
}
