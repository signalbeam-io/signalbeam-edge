include "root" {
  path = find_in_parent_folders()
}

terraform {
  source = "../../../terraform/modules/static-web-app"
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

  # SWA Free tier is not offered in northeurope (where the ACA stack lives).
  # Pin to West Europe — overrides the env.hcl location for this module only.
  location = "westeurope"
}
