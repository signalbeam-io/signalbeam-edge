include "root" {
  path = find_in_parent_folders()
}

locals {
  env  = read_terragrunt_config(find_in_parent_folders("env.hcl")).locals
  name = "${local.env.project}-ca-nats-${local.env.environment}"
}

terraform {
  source = "../../../../terraform/modules/container-app"
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

dependency "managed_identity" {
  config_path = "../../managed-identity"

  mock_outputs = {
    id           = "/subscriptions/00000000/resourceGroups/mock-rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/mock-id"
    principal_id = "00000000-0000-0000-0000-000000000000"
    client_id    = "00000000-0000-0000-0000-000000000000"
    name         = "mock-id"
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
}

dependency "environment" {
  config_path = "../environment"

  mock_outputs = {
    id                = "/subscriptions/00000000/resourceGroups/mock-rg/providers/Microsoft.App/managedEnvironments/mock-cae"
    name              = "mock-cae"
    default_domain    = "mockenv.westeurope.azurecontainerapps.io"
    static_ip_address = "20.0.0.1"
    nats_storage_name = "nats-jetstream"
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
}

inputs = {
  name                         = local.name
  resource_group_name          = dependency.resource_group.outputs.name
  container_app_environment_id = dependency.environment.outputs.id
  managed_identity_id          = dependency.managed_identity.outputs.id

  # Public upstream image — no private registry credentials needed.
  image = "nats:2.10-alpine"
  # --no_advertise stops NATS from gossiping its internal pod IP to clients in
  # the INFO handshake; behind the ACA Envoy TCP proxy that advertised IP is
  # unreachable, which can break NATS .NET client connections.
  command = ["nats-server", "--jetstream", "--store_dir=/data", "--http_port=8222", "--no_advertise"]

  # The one always-on service: persistent JetStream, cannot scale to zero.
  min_replicas = 1
  max_replicas = 1

  # Internal TCP ingress on the NATS client port; other apps connect via
  # nats://<name>.internal.<default-domain>:4222.
  ingress_external = false
  transport        = "tcp"
  target_port      = 4222

  # ACA can't HTTP-probe a TCP ingress port, so the liveness probe targets the
  # NATS monitoring endpoint (/healthz on 8222) to catch a hung JetStream.
  liveness_probe_path = "/healthz"
  liveness_probe_port = 8222

  # TCP startup + readiness probes on the client port so ACA confirms 4222 is
  # accepting and opens the internal TCP ingress listener (otherwise app-to-app
  # connections to nats://...:4222 time out even though NATS is healthy).
  tcp_probe_port = 4222

  # JetStream persistence on the Azure Files share registered with the environment.
  volumes = [
    { name = "nats-data", storage_name = dependency.environment.outputs.nats_storage_name, path = "/data" },
  ]
}
