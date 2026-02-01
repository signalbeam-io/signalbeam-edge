include "root" {
  path = find_in_parent_folders()
}

terraform {
  source = "../../../terraform/modules/aks-cluster"
}

dependency "resource_group" {
  config_path = "../resource-group"
}

dependency "networking" {
  config_path = "../networking"
}

dependency "monitoring" {
  config_path = "../monitoring"
}

dependency "container_registry" {
  config_path = "../container-registry"
}

dependency "managed_identity" {
  config_path = "../managed-identity"
}

dependency "key_vault" {
  config_path = "../key-vault"
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
