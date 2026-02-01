include "root" {
  path = find_in_parent_folders()
}

terraform {
  source = "../../../terraform/modules/postgresql"
}

dependency "resource_group" {
  config_path = "../resource-group"

  mock_outputs = {
    name     = "mock-rg"
    id       = "/subscriptions/00000000/resourceGroups/mock-rg"
    location = "westeurope"
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
}

dependency "networking" {
  config_path = "../networking"

  mock_outputs = {
    vnet_id                        = "/subscriptions/00000000/resourceGroups/mock-rg/providers/Microsoft.Network/virtualNetworks/mock-vnet"
    vnet_name                      = "mock-vnet"
    aks_subnet_id                  = "/subscriptions/00000000/resourceGroups/mock-rg/providers/Microsoft.Network/virtualNetworks/mock-vnet/subnets/snet-aks"
    postgresql_subnet_id           = "/subscriptions/00000000/resourceGroups/mock-rg/providers/Microsoft.Network/virtualNetworks/mock-vnet/subnets/snet-postgresql"
    services_subnet_id             = "/subscriptions/00000000/resourceGroups/mock-rg/providers/Microsoft.Network/virtualNetworks/mock-vnet/subnets/snet-services"
    postgresql_private_dns_zone_id = "/subscriptions/00000000/resourceGroups/mock-rg/providers/Microsoft.Network/privateDnsZones/privatelink.postgres.database.azure.com"
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
}

dependency "key_vault" {
  config_path = "../key-vault"

  mock_outputs = {
    id                        = "/subscriptions/00000000/resourceGroups/mock-rg/providers/Microsoft.KeyVault/vaults/mock-kv"
    name                      = "mock-kv"
    vault_uri                 = "https://mock-kv.vault.azure.net/"
    postgresql_admin_password = "mock-password"
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
}

inputs = {
  resource_group_name    = dependency.resource_group.outputs.name
  delegated_subnet_id    = dependency.networking.outputs.postgresql_subnet_id
  private_dns_zone_id    = dependency.networking.outputs.postgresql_private_dns_zone_id
  administrator_password = dependency.key_vault.outputs.postgresql_admin_password
}
