include "root" {
  path = find_in_parent_folders()
}

terraform {
  source = "../../../terraform/modules/cert-manager"
}

dependency "aks_cluster" {
  config_path = "../aks-cluster"

  mock_outputs = {
    name            = "mock-aks"
    kube_config_raw = "mock-kubeconfig"
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
}

dependency "managed_identity" {
  config_path = "../managed-identity"

  mock_outputs = {
    client_id = "00000000-0000-0000-0000-000000000000"
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
}

dependency "dns" {
  config_path = "../dns"

  mock_outputs = {
    name = "dev.signalbeam.io"
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
}

dependency "resource_group" {
  config_path = "../resource-group"

  mock_outputs = {
    name = "mock-rg"
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
}

inputs = {
  kube_config_raw             = dependency.aks_cluster.outputs.kube_config_raw
  workload_identity_client_id = dependency.managed_identity.outputs.client_id
  dns_zone_name               = dependency.dns.outputs.name
  dns_zone_resource_group     = dependency.resource_group.outputs.name
}
