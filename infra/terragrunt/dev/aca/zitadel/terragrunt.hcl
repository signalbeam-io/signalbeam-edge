include "root" {
  path = find_in_parent_folders()
}

locals {
  env  = read_terragrunt_config(find_in_parent_folders("env.hcl")).locals
  name = "${local.env.project}-ca-zitadel-${local.env.environment}"
  # The external FQDN is computed inline in env_vars below — a locals block can
  # only reference other locals, not dependency outputs.
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

dependency "postgresql" {
  config_path = "../../postgresql"

  mock_outputs = {
    id             = "/subscriptions/00000000/resourceGroups/mock-rg/providers/Microsoft.DBforPostgreSQL/flexibleServers/mock-psql"
    name           = "mock-psql"
    fqdn           = "mock-psql.postgres.database.azure.com"
    database_names = ["signalbeam", "zitadel"]
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
}

dependency "secrets" {
  config_path = "../secrets"

  mock_outputs = {
    db_connection_secret_id           = "https://mock-kv.vault.azure.net/secrets/db-connection-signalbeam"
    ghcr_pat_secret_id                = "https://mock-kv.vault.azure.net/secrets/ghcr-pat"
    zitadel_master_key_secret_id      = "https://mock-kv.vault.azure.net/secrets/zitadel-master-key"
    postgres_admin_password_secret_id = "https://mock-kv.vault.azure.net/secrets/postgresql-admin-password"
    zitadel_admin_password_secret_id  = "https://mock-kv.vault.azure.net/secrets/zitadel-admin-password"
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
}

# Migration/bootstrap job. Declaring it here orders the `terragrunt run --all`
# DAG so the schema-building `zitadel setup` job is created before this service.
# The job still has to be EXECUTED (`az containerapp job start`) and finish
# before this service can serve — see this unit's args comment.
dependency "zitadel_setup" {
  config_path = "../zitadel-setup"

  mock_outputs = {
    id   = "/subscriptions/00000000/resourceGroups/mock-rg/providers/Microsoft.App/jobs/mock-caj"
    name = "mock-caj-zitadel-setup"
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
}

inputs = {
  name                         = local.name
  resource_group_name          = dependency.resource_group.outputs.name
  container_app_environment_id = dependency.environment.outputs.id
  managed_identity_id          = dependency.managed_identity.outputs.id

  # Pinned upstream image (public — no registry credentials). v2.66.3 matches the
  # version used by the Aspire AppHost for local dev, so behaviour is reproducible.
  image = "ghcr.io/zitadel/zitadel:v2.66.3"

  # `start` ONLY runs the long-running HTTP server — it does NOT run migrations.
  # All schema/migration + first-instance work is done by the separate
  # `zitadel-setup` Container App Job (args = ["setup"]), which must finish before
  # this service is deployed/restarted. Because the service no longer migrates,
  # overlapping replicas during an ACA rolling revision can never race the
  # 03_default_instance migration — which is the deadlock the combined
  # `start-from-init` used to cause.
  #
  # --tlsMode external (NOT disabled): TLS is terminated by the ACA Envoy ingress,
  # so Zitadel serves plain HTTP internally — but `external` tells Zitadel the
  # EXTERNAL scheme is https and to trust X-Forwarded-Proto. With `disabled`,
  # Zitadel builds the console's `environment.json` `api` URL from the (plain http)
  # request scheme, so it emits http:// while the issuer is https:// — the console
  # (loaded over https) then violates its own CSP (`connect-src` matches the host
  # on https only) when it calls the http api. `external` makes the api URL https
  # and consistent. (ExternalSecure=true alone only fixes the issuer, not the api.)
  # --masterkeyFromEnv reads the 32-char master key from the ZITADEL_MASTERKEY env var.
  args = ["start", "--masterkeyFromEnv", "--tlsMode", "external"]

  # Zitadel is stateful at startup and behaves poorly with scale-to-zero cold
  # starts (it must stay reachable for token/JWKS validation by other services).
  min_replicas = 1
  max_replicas = 1

  cpu    = 0.5
  memory = "1.0Gi"

  # External ingress: the browser and backend services reach Zitadel directly at
  # its own FQDN. http2 so the gRPC/gRPC-Web management + /v2 APIs work (used by
  # the bootstrap tool); HTTP/1.1 OIDC/login traffic still negotiates fine.
  ingress_external = true
  transport        = "http2"
  target_port      = 8080

  # With migrations moved to the `zitadel-setup` job, `start` brings the HTTP
  # server up quickly (no multi-minute migration before /debug/ready), so a
  # readiness probe is safe again: the new revision reports ready within the
  # normal window and the old one retires cleanly. This also lets ACA route
  # traffic only once Zitadel can actually serve token/JWKS validation.
  readiness_probe_path = "/debug/ready"

  # `start` connects as the application (user) role only — DB/role creation and
  # the admin connection live in the setup job, so no admin password here.
  kv_secrets = [
    { name = "zitadel-master-key", key_vault_secret_id = dependency.secrets.outputs.zitadel_master_key_secret_id },
    { name = "zitadel-db-password", key_vault_secret_id = dependency.secrets.outputs.postgres_admin_password_secret_id },
  ]

  # Secret-sourced env vars. Master key is passed via env (not the --masterkey
  # arg) to avoid shell-quoting issues with special characters — requires the
  # --masterkeyFromEnv flag in args above for Zitadel to actually read it.
  secret_env_vars = {
    "ZITADEL_MASTERKEY"                       = "zitadel-master-key"
    "ZITADEL_DATABASE_POSTGRES_USER_PASSWORD" = "zitadel-db-password"
  }

  env_vars = {
    # --- Database (shared Postgres Flexible Server, dedicated `zitadel` DB) ---
    # User (application) connection only — admin connection is used by the setup job.
    "ZITADEL_DATABASE_POSTGRES_HOST"          = dependency.postgresql.outputs.fqdn
    "ZITADEL_DATABASE_POSTGRES_PORT"          = "5432"
    "ZITADEL_DATABASE_POSTGRES_DATABASE"      = "zitadel"
    "ZITADEL_DATABASE_POSTGRES_USER_USERNAME" = "pgadmin"
    "ZITADEL_DATABASE_POSTGRES_USER_SSL_MODE" = "require"

    # --- External addressing (issuer/discovery host) ---
    # ACA assigns an external app the FQDN "<name>.<env-default-domain>" (internal
    # apps get "<name>.internal.<...>"), predictable before the app exists — so
    # EXTERNALDOMAIN is set without a self-referential output. It MUST equal the
    # browser-facing host AND the value the setup job used, or OIDC discovery +
    # JWT issuer validation break.
    "ZITADEL_EXTERNALSECURE" = "true"
    "ZITADEL_EXTERNALDOMAIN" = "${local.name}.${dependency.environment.outputs.default_domain}"
    "ZITADEL_EXTERNALPORT"   = "443"

    # --- Machine ID (sonyflake) identification ---
    # Zitadel derives a per-machine sonyflake ID at startup. Its defaults
    # (Private IP + GCP-metadata webhook) BOTH fail on ACA — the container has no
    # private IP Zitadel can read and there's no GCP metadata server — so it
    # panics: "none of the enabled methods for identifying the machine
    # succeeded". ACA gives each replica a unique hostname (like a K8s pod), so
    # switch to hostname identification and disable the two failing methods.
    "ZITADEL_MACHINE_IDENTIFICATION_HOSTNAME_ENABLED"  = "true"
    "ZITADEL_MACHINE_IDENTIFICATION_PRIVATEIP_ENABLED" = "false"
    "ZITADEL_MACHINE_IDENTIFICATION_WEBHOOK_ENABLED"   = "false"
  }
}
