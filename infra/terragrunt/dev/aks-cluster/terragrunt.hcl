include "root" {
  path = find_in_parent_folders()
}

terraform {
  source = "../../../terraform/modules/aks-cluster"
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

dependency "monitoring" {
  config_path = "../monitoring"

  mock_outputs = {
    log_analytics_workspace_id   = "/subscriptions/00000000/resourceGroups/mock-rg/providers/Microsoft.OperationalInsights/workspaces/mock-law"
    log_analytics_workspace_name = "mock-law"
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
}

dependency "container_registry" {
  config_path = "../container-registry"

  mock_outputs = {
    id           = "/subscriptions/00000000/resourceGroups/mock-rg/providers/Microsoft.ContainerRegistry/registries/mockacr"
    name         = "mockacr"
    login_server = "mockacr.azurecr.io"
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
  resource_group_name         = dependency.resource_group.outputs.name
  aks_subnet_id               = dependency.networking.outputs.aks_subnet_id
  container_registry_id       = dependency.container_registry.outputs.id
  log_analytics_workspace_id  = dependency.monitoring.outputs.log_analytics_workspace_id
  workload_identity_id        = dependency.managed_identity.outputs.id
  workload_identity_client_id = dependency.managed_identity.outputs.client_id
  key_vault_id                = dependency.key_vault.outputs.id
}
