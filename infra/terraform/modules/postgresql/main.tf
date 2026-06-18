locals {
  location_short = lookup({ westeurope = "weu", northeurope = "neu" }, var.location, "weu")
}

resource "azurerm_postgresql_flexible_server" "this" {
  name                          = "${var.project}-psql-${var.environment}-${local.location_short}"
  resource_group_name           = var.resource_group_name
  location                      = var.location
  version                       = var.postgresql_version
  administrator_login           = "pgadmin"
  administrator_password        = var.administrator_password
  sku_name                      = var.sku_name
  storage_mb                    = var.storage_mb
  backup_retention_days         = 7
  geo_redundant_backup_enabled  = false
  public_network_access_enabled = false
  delegated_subnet_id           = var.delegated_subnet_id
  private_dns_zone_id           = var.private_dns_zone_id
  zone                          = "1"

  tags = var.tags

  lifecycle {
    ignore_changes = [zone]
  }
}

# --- Databases ---

resource "azurerm_postgresql_flexible_server_database" "databases" {
  for_each  = toset(var.databases)
  name      = each.value
  server_id = azurerm_postgresql_flexible_server.this.id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

# --- Server Configuration ---

resource "azurerm_postgresql_flexible_server_configuration" "extensions" {
  server_id = azurerm_postgresql_flexible_server.this.id
  name      = "azure.extensions"
  value     = "TIMESCALEDB,UUID-OSSP,PG_STAT_STATEMENTS"
}

resource "azurerm_postgresql_flexible_server_configuration" "log_min_duration" {
  server_id = azurerm_postgresql_flexible_server.this.id
  name      = "log_min_duration_statement"
  value     = "1000" # 1 second
}

resource "azurerm_postgresql_flexible_server_configuration" "require_secure_transport" {
  server_id = azurerm_postgresql_flexible_server.this.id
  name      = "require_secure_transport"
  value     = "on"
}

resource "azurerm_postgresql_flexible_server_configuration" "ssl_min_protocol_version" {
  server_id = azurerm_postgresql_flexible_server.this.id
  name      = "ssl_min_protocol_version"
  value     = "TLSv1.2"
}
