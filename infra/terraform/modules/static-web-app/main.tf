locals {
  location_short = lookup({ westeurope = "weu", northeurope = "neu" }, var.location, "weu")
  name           = "${var.project}-swa-web-${var.environment}-${local.location_short}"
}

# Azure Static Web Apps hosts the React dashboard (web/) on a permanent
# *.azurestaticapps.net URL. The Free tier is sufficient for the dev dashboard.
#
# Note: SWA is only offered in a handful of regions and is NOT available in
# northeurope (the rest of the lean ACA stack lives there). The location is
# pinned to West Europe via the terragrunt wrapper — see variables.tf default.
resource "azurerm_static_web_app" "this" {
  name                = local.name
  resource_group_name = var.resource_group_name
  location            = var.location
  sku_tier            = var.sku_tier
  sku_size            = var.sku_size

  tags = var.tags
}
