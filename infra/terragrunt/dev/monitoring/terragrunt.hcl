include "root" {
  path = find_in_parent_folders()
}

terraform {
  source = "../../../terraform/modules/monitoring"
}

dependency "resource_group" {
  config_path = "../resource-group"
}

inputs = {
  resource_group_name = dependency.resource_group.outputs.name
}
