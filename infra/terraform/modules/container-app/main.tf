# Reusable Azure Container App.
#
# Designed for the SignalBeam control plane on ACA:
#   - User-assigned managed identity for Key Vault references and registry auth
#   - Secrets sourced as Key Vault references (values never enter Terraform state)
#   - Private GHCR registry via a PAT held as a (KV-referenced) secret
#   - External ingress for the ApiGateway, internal ingress for everything else
#   - Scale-to-zero by default (min_replicas = 0); stateful apps set min_replicas = 1
#   - Optional Azure Files volume mounts (NATS JetStream persistence)
resource "azurerm_container_app" "this" {
  name                         = var.name
  container_app_environment_id = var.container_app_environment_id
  resource_group_name          = var.resource_group_name
  revision_mode                = var.revision_mode
  workload_profile_name        = var.workload_profile_name
  tags                         = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [var.managed_identity_id]
  }

  # Private container registry (e.g. GHCR). The password is a secret defined
  # below and referenced here by name.
  dynamic "registry" {
    for_each = var.registry_server == "" ? [] : [1]
    content {
      server               = var.registry_server
      username             = var.registry_username
      password_secret_name = var.registry_password_secret_name
    }
  }

  # Key Vault reference secrets — the value stays in Key Vault and is resolved at
  # runtime by the managed identity (which holds "Key Vault Secrets User").
  dynamic "secret" {
    for_each = var.kv_secrets
    content {
      name                = secret.value.name
      key_vault_secret_id = secret.value.key_vault_secret_id
      identity            = var.managed_identity_id
    }
  }

  template {
    min_replicas = var.min_replicas
    max_replicas = var.max_replicas

    dynamic "volume" {
      for_each = var.volumes
      content {
        name         = volume.value.name
        storage_name = volume.value.storage_name
        storage_type = "AzureFile"
      }
    }

    container {
      name    = var.name
      image   = var.image
      cpu     = var.cpu
      memory  = var.memory
      command = var.command

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

      dynamic "volume_mounts" {
        for_each = var.volumes
        content {
          name = volume_mounts.value.name
          path = volume_mounts.value.path
        }
      }

      # Liveness uses /health/live (no dependency checks) so a transient DB/NATS
      # blip does not trigger container restarts.
      dynamic "liveness_probe" {
        for_each = var.liveness_probe_path == "" ? [] : [1]
        content {
          transport     = "HTTP"
          port          = var.target_port
          path          = var.liveness_probe_path
          initial_delay = 15
        }
      }

      # Readiness uses /health/ready (checks dependencies) so traffic is only
      # routed once the app can serve it.
      dynamic "readiness_probe" {
        for_each = var.readiness_probe_path == "" ? [] : [1]
        content {
          transport        = "HTTP"
          port             = var.target_port
          path             = var.readiness_probe_path
          interval_seconds = 15
        }
      }
    }
  }

  dynamic "ingress" {
    for_each = var.ingress_enabled ? [1] : []
    content {
      external_enabled           = var.ingress_external
      target_port                = var.target_port
      exposed_port               = var.transport == "tcp" ? var.target_port : null
      transport                  = var.transport
      allow_insecure_connections = var.allow_insecure_connections

      traffic_weight {
        latest_revision = true
        percentage      = 100
      }
    }
  }
}
