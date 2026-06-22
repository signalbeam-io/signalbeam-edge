# Reusable Azure Container App Job — a one-shot, run-to-completion task on the
# same Container Apps environment as the long-running apps.
#
# Designed for bootstrap/migration work (e.g. Zitadel `setup`) that must run
# exactly once per deploy and finish before the long-running service starts. By
# moving migrations out of the service and into a Job with parallelism = 1, a
# rolling service revision can never run two replicas that race the same
# migration. Mirrors the container-app module for identity, secrets, and
# registry handling so a service and its migration job share configuration.
resource "azurerm_container_app_job" "this" {
  name                         = var.name
  location                     = var.location
  resource_group_name          = var.resource_group_name
  container_app_environment_id = var.container_app_environment_id
  workload_profile_name        = var.workload_profile_name
  tags                         = var.tags

  # A job execution is killed if it runs past this; one Zitadel setup is well
  # under this on a warm DB but the first run does the full schema build.
  replica_timeout_in_seconds = var.replica_timeout_in_seconds
  replica_retry_limit        = var.replica_retry_limit

  # Manually triggered: applied by Terraform, then executed on demand via
  # `az containerapp job start` in the deploy pipeline before the service rolls.
  # parallelism = 1 + replica_completion_count = 1 means a single execution runs
  # exactly one replica — so the job can never race itself on the migration.
  manual_trigger_config {
    parallelism              = 1
    replica_completion_count = 1
  }

  identity {
    type         = "UserAssigned"
    identity_ids = [var.managed_identity_id]
  }

  dynamic "registry" {
    for_each = var.registry_server == "" ? [] : [1]
    content {
      server               = var.registry_server
      username             = var.registry_username
      password_secret_name = var.registry_password_secret_name
    }
  }

  # Key Vault reference secrets — resolved at runtime by the managed identity.
  dynamic "secret" {
    for_each = var.kv_secrets
    content {
      name                = secret.value.name
      key_vault_secret_id = secret.value.key_vault_secret_id
      identity            = var.managed_identity_id
    }
  }

  template {
    container {
      name   = var.name
      image  = var.image
      cpu    = var.cpu
      memory = var.memory
      # Use null (not []) when unset so the image's own entrypoint/CMD is preserved.
      command = length(var.command) > 0 ? var.command : null
      args    = length(var.args) > 0 ? var.args : null

      # Plain environment variables
      dynamic "env" {
        for_each = var.env_vars
        content {
          name  = env.key
          value = env.value
        }
      }

      # Environment variables sourced from secrets (env name -> secret name)
      dynamic "env" {
        for_each = var.secret_env_vars
        content {
          name        = env.key
          secret_name = env.value
        }
      }
    }
  }
}
