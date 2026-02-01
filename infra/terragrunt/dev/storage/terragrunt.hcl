include "root" {
  path = find_in_parent_folders()
}

terraform {
  source = "../../../terraform/modules/storage"
}

dependency "resource_group" {
  config_path = "../resource-group"
}

dependency "networking" {
  config_path = "../networking"
}

dependency "managed_identity" {
  config_path = "../managed-identity"
}

inputs = {
  resource_group_name            = dependency.resource_group.outputs.name
  aks_subnet_id                  = dependency.networking.outputs.aks_subnet_id
  workload_identity_principal_id = dependency.managed_identity.outputs.principal_id
}
