include "root" {
  path = find_in_parent_folders()
}

terraform {
  source = "../../../terraform/modules/managed-identity"
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

inputs = {
  resource_group_name = dependency.resource_group.outputs.name
}
