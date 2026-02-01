include "root" {
  path = find_in_parent_folders()
}

terraform {
  source = "../../../terraform/modules/storage"
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

dependency "managed_identity" {
  config_path = "../managed-identity"

  mock_outputs = {
    id           = "/subscriptions/00000000/resourceGroups/mock-rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/mock-id"
    principal_id = "00000000-0000-0000-0000-000000000000"
    client_id    = "00000000-0000-0000-0000-000000000000"
    name         = "mock-id"
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
}

inputs = {
  resource_group_name            = dependency.resource_group.outputs.name
  aks_subnet_id                  = dependency.networking.outputs.aks_subnet_id
  workload_identity_principal_id = dependency.managed_identity.outputs.principal_id
}
