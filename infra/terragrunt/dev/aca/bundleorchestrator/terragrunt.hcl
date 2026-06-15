include "root" {
  path = find_in_parent_folders()
}

locals {
  env  = read_terragrunt_config(find_in_parent_folders("env.hcl")).locals
  name = "${local.env.project}-ca-bundleorchestrator-${local.env.environment}"
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

dependency "secrets" {
  config_path = "../secrets"

  mock_outputs = {
    db_connection_secret_id = "https://mock-kv.vault.azure.net/secrets/db-connection-signalbeam"
    ghcr_pat_secret_id      = "https://mock-kv.vault.azure.net/secrets/ghcr-pat"
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
}

inputs = {
  name                         = local.name
  resource_group_name          = dependency.resource_group.outputs.name
  container_app_environment_id = dependency.environment.outputs.id
  managed_identity_id          = dependency.managed_identity.outputs.id
  image                        = "ghcr.io/signalbeam-io/bundleorchestrator:latest"

  ingress_external     = false
  target_port          = 8080
  liveness_probe_path  = "/health/live"
  readiness_probe_path = "/health/ready"

  registry_server               = "ghcr.io"
  registry_username             = "signalbeam-io"
  registry_password_secret_name = "ghcr-pat"

  kv_secrets = [
    { name = "ghcr-pat", key_vault_secret_id = dependency.secrets.outputs.ghcr_pat_secret_id },
    { name = "db-conn", key_vault_secret_id = dependency.secrets.outputs.db_connection_secret_id },
  ]

  secret_env_vars = {
    "ConnectionStrings__signalbeam" = "db-conn"
  }

  env_vars = {
    "ASPNETCORE_HTTP_PORTS" = "8080"
    "NATS__Url"             = "nats://${local.env.project}-ca-nats-${local.env.environment}.internal.${dependency.environment.outputs.default_domain}:4222"
  }
}
