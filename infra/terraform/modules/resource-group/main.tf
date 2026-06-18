locals {
  location_short = lookup({ westeurope = "weu", northeurope = "neu" }, var.location, "weu")
  name           = "${var.project}-rg-${var.environment}-${local.location_short}"
}

resource "azurerm_resource_group" "this" {
  name     = local.name
  location = var.location
  tags     = var.tags
}
