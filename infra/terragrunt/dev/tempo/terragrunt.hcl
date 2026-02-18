include "root" {
  path = find_in_parent_folders()
}

terraform {
  source = "../../../terraform/modules/tempo"
}

dependency "aks_cluster" {
  config_path = "../aks-cluster"

  mock_outputs = {
    name            = "mock-aks"
    kube_config_raw = "mock-kubeconfig"
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
}

dependency "kube_prometheus_stack" {
  config_path = "../kube-prometheus-stack"

  mock_outputs = {
    namespace = "monitoring"
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
}

inputs = {
  kube_config_raw = dependency.aks_cluster.outputs.kube_config_raw
  namespace       = dependency.kube_prometheus_stack.outputs.namespace
}
