include "root" {
  path = find_in_parent_folders()
}

terraform {
  source = "../../../terraform/modules/dns"
}

dependency "resource_group" {
  config_path = "../resource-group"
}

dependency "managed_identity" {
  config_path = "../managed-identity"
}

inputs = {
  resource_group_name            = dependency.resource_group.outputs.name
  workload_identity_principal_id = dependency.managed_identity.outputs.principal_id
}
