include "root" {
  path = find_in_parent_folders()
}

terraform {
  source = "../../../../terraform/modules/container-app-environment"
}

dependency "resource_group" {
  config_path = "../../resource-group"

  mock_outputs = {
    name     = "mock-rg"
    id       = "/subscriptions/00000000/resourceGroups/mock-rg"
    location = "westeurope"
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
}

dependency "networking" {
  config_path = "../../networking"

  mock_outputs = {
    vnet_id                        = "/subscriptions/00000000/resourceGroups/mock-rg/providers/Microsoft.Network/virtualNetworks/mock-vnet"
    vnet_name                      = "mock-vnet"
    aks_subnet_id                  = "/subscriptions/00000000/resourceGroups/mock-rg/providers/Microsoft.Network/virtualNetworks/mock-vnet/subnets/snet-aks"
    postgresql_subnet_id           = "/subscriptions/00000000/resourceGroups/mock-rg/providers/Microsoft.Network/virtualNetworks/mock-vnet/subnets/snet-postgresql"
    services_subnet_id             = "/subscriptions/00000000/resourceGroups/mock-rg/providers/Microsoft.Network/virtualNetworks/mock-vnet/subnets/snet-services"
    aca_subnet_id                  = "/subscriptions/00000000/resourceGroups/mock-rg/providers/Microsoft.Network/virtualNetworks/mock-vnet/subnets/snet-aca"
    postgresql_private_dns_zone_id = "/subscriptions/00000000/resourceGroups/mock-rg/providers/Microsoft.Network/privateDnsZones/privatelink.postgres.database.azure.com"
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
}

dependency "monitoring" {
  config_path = "../../monitoring"

  mock_outputs = {
    log_analytics_workspace_id   = "/subscriptions/00000000/resourceGroups/mock-rg/providers/Microsoft.OperationalInsights/workspaces/mock-law"
    log_analytics_workspace_name = "mock-law"
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
}

inputs = {
  resource_group_name        = dependency.resource_group.outputs.name
  log_analytics_workspace_id = dependency.monitoring.outputs.log_analytics_workspace_id
  infrastructure_subnet_id   = dependency.networking.outputs.aca_subnet_id
  aca_subnet_id              = dependency.networking.outputs.aca_subnet_id
}
