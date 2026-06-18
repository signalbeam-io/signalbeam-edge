locals {
  location_short = lookup({ westeurope = "weu", northeurope = "neu" }, var.location, "weu")
  name_prefix    = "${var.project}-${var.environment}-${local.location_short}"
}

# --- Virtual Network ---

resource "azurerm_virtual_network" "this" {
  name                = "${var.project}-vnet-${var.environment}-${local.location_short}"
  location            = var.location
  resource_group_name = var.resource_group_name
  address_space       = var.vnet_address_space
  tags                = var.tags
}

# --- Subnets ---

resource "azurerm_subnet" "aks" {
  name                 = "snet-aks"
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.this.name
  address_prefixes     = [var.aks_subnet_prefix]
  # Required before the Key Vault can ACL this subnet (network_acls in key-vault module).
  service_endpoints = ["Microsoft.KeyVault"]
}

resource "azurerm_subnet" "postgresql" {
  name                 = "snet-postgresql"
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.this.name
  address_prefixes     = [var.postgresql_subnet_prefix]

  delegation {
    name = "postgresql-delegation"
    service_delegation {
      name = "Microsoft.DBforPostgreSQL/flexibleServers"
      actions = [
        "Microsoft.Network/virtualNetworks/subnets/join/action",
      ]
    }
  }
}

resource "azurerm_subnet" "services" {
  name                 = "snet-services"
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.this.name
  address_prefixes     = [var.services_subnet_prefix]
  # Required before the Key Vault can ACL this subnet (network_acls in key-vault module).
  service_endpoints = ["Microsoft.KeyVault"]
}

# Dedicated subnet for the Azure Container Apps (Consumption) environment.
# A workload-profile/Consumption environment requires its own subnet delegated
# to Microsoft.App/environments. Minimum size is /27 (Consumption-only); we use
# the configured prefix. The subnet must not carry an NSG rule that blocks the
# ACA control-plane ports, so we leave it un-associated with a deny-all NSG.
resource "azurerm_subnet" "aca" {
  name                 = "snet-aca"
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.this.name
  address_prefixes     = [var.aca_subnet_prefix]
  # Service endpoints so the ACA apps can reach the NATS Azure Files storage
  # account over the VNet (Storage), and so the Key Vault can ACL this subnet
  # (KeyVault — see network_acls in the key-vault module). Both are required
  # while those resources deny public network access.
  service_endpoints = ["Microsoft.Storage", "Microsoft.KeyVault"]

  delegation {
    name = "aca-delegation"
    service_delegation {
      name = "Microsoft.App/environments"
      actions = [
        "Microsoft.Network/virtualNetworks/subnets/join/action",
      ]
    }
  }
}

# --- Network Security Groups ---

resource "azurerm_network_security_group" "aks" {
  name                = "${var.project}-nsg-aks-${var.environment}-${local.location_short}"
  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = var.tags
}

resource "azurerm_network_security_group" "postgresql" {
  name                = "${var.project}-nsg-psql-${var.environment}-${local.location_short}"
  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = var.tags
}

resource "azurerm_network_security_rule" "postgresql_allow_aks" {
  name                        = "AllowAksToPostgreSQL"
  priority                    = 100
  direction                   = "Inbound"
  access                      = "Allow"
  protocol                    = "Tcp"
  source_port_range           = "*"
  destination_port_range      = "5432"
  source_address_prefix       = var.aks_subnet_prefix
  destination_address_prefix  = var.postgresql_subnet_prefix
  resource_group_name         = var.resource_group_name
  network_security_group_name = azurerm_network_security_group.postgresql.name
}

resource "azurerm_network_security_rule" "postgresql_allow_aca" {
  name                        = "AllowAcaToPostgreSQL"
  priority                    = 110
  direction                   = "Inbound"
  access                      = "Allow"
  protocol                    = "Tcp"
  source_port_range           = "*"
  destination_port_range      = "5432"
  source_address_prefix       = var.aca_subnet_prefix
  destination_address_prefix  = var.postgresql_subnet_prefix
  resource_group_name         = var.resource_group_name
  network_security_group_name = azurerm_network_security_group.postgresql.name
}

resource "azurerm_network_security_rule" "postgresql_deny_all" {
  name                        = "DenyAllInbound"
  priority                    = 4096
  direction                   = "Inbound"
  access                      = "Deny"
  protocol                    = "*"
  source_port_range           = "*"
  destination_port_range      = "*"
  source_address_prefix       = "*"
  destination_address_prefix  = "*"
  resource_group_name         = var.resource_group_name
  network_security_group_name = azurerm_network_security_group.postgresql.name
}

resource "azurerm_network_security_group" "services" {
  name                = "${var.project}-nsg-svc-${var.environment}-${local.location_short}"
  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = var.tags
}

resource "azurerm_network_security_rule" "services_allow_aks_https" {
  name                        = "AllowAksToServicesHttps"
  priority                    = 100
  direction                   = "Inbound"
  access                      = "Allow"
  protocol                    = "Tcp"
  source_port_range           = "*"
  destination_port_range      = "443"
  source_address_prefix       = var.aks_subnet_prefix
  destination_address_prefix  = var.services_subnet_prefix
  resource_group_name         = var.resource_group_name
  network_security_group_name = azurerm_network_security_group.services.name
}

resource "azurerm_network_security_rule" "services_deny_all" {
  name                        = "DenyAllInbound"
  priority                    = 4096
  direction                   = "Inbound"
  access                      = "Deny"
  protocol                    = "*"
  source_port_range           = "*"
  destination_port_range      = "*"
  source_address_prefix       = "*"
  destination_address_prefix  = "*"
  resource_group_name         = var.resource_group_name
  network_security_group_name = azurerm_network_security_group.services.name
}

# --- NSG Associations ---

resource "azurerm_subnet_network_security_group_association" "aks" {
  subnet_id                 = azurerm_subnet.aks.id
  network_security_group_id = azurerm_network_security_group.aks.id
}

resource "azurerm_subnet_network_security_group_association" "postgresql" {
  subnet_id                 = azurerm_subnet.postgresql.id
  network_security_group_id = azurerm_network_security_group.postgresql.id
}

resource "azurerm_subnet_network_security_group_association" "services" {
  subnet_id                 = azurerm_subnet.services.id
  network_security_group_id = azurerm_network_security_group.services.id
}

# --- Private DNS Zone for PostgreSQL ---

resource "azurerm_private_dns_zone" "postgresql" {
  name                = "privatelink.postgres.database.azure.com"
  resource_group_name = var.resource_group_name
  tags                = var.tags
}

resource "azurerm_private_dns_zone_virtual_network_link" "postgresql" {
  name                  = "psql-dns-link"
  resource_group_name   = var.resource_group_name
  private_dns_zone_name = azurerm_private_dns_zone.postgresql.name
  virtual_network_id    = azurerm_virtual_network.this.id
  registration_enabled  = false
  tags                  = var.tags
}
