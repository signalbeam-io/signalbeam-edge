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

inputs = {
  name                         = local.name
  resource_group_name          = dependency.resource_group.outputs.name
  container_app_environment_id = dependency.environment.outputs.id
  managed_identity_id          = dependency.managed_identity.outputs.id

  # Pinned upstream image (public — no registry credentials). v2.66.3 matches the
  # version used by the Aspire AppHost for local dev, so behaviour is reproducible.
  image = "ghcr.io/zitadel/zitadel:v2.66.3"

  # start-from-init runs DB setup/migrations then starts, in one idempotent
  # process. TLS is terminated by the ACA Envoy ingress, so Zitadel serves plain
  # HTTP (h2c) internally.
  #
  # These are ARGS, not command: the image ENTRYPOINT is the /app/zitadel binary
  # and "start-from-init" is a subcommand of it. Using `command` would override
  # the entrypoint and try to exec "start-from-init" as a standalone executable
  # (which fails: "executable file not found in $PATH").
  #
  # --masterkeyFromEnv tells Zitadel to read the master key from the
  # ZITADEL_MASTERKEY env var (wired below as a KV secret). Without this flag it
  # only checks --masterkey/--masterkeyFile and panics "No master key provided".
  args = ["start-from-init", "--masterkeyFromEnv", "--tlsMode", "disabled"]

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

  # NO probes on purpose. start-from-init runs DB migrations BEFORE the HTTP
  # server starts, so /debug/ready stays down for ~1–2 min on first boot. With a
  # readiness probe, ACA never marks the new revision "ready", so in single
  # revision mode it never retires the OLD revision — every redeploy then leaves
  # another Zitadel replica running, and multiple replicas racing start-from-init
  # deadlock on the 03_default_instance migration ("migration already started").
  # Dropping the probe lets a new revision go active immediately and the old one
  # retire, so exactly one Zitadel runs the migration. (No liveness either — the
  # process doesn't self-exit, and we don't want restarts mid-migration.)
  #
  # NOTE: the proper long-term fix is to split init into a one-shot `zitadel
  # setup` Job and run `zitadel start` (no migrations) as the service, so
  # overlapping replicas during a rollout can never race the migration.
  readiness_probe_path = ""

  kv_secrets = [
    { name = "zitadel-master-key", key_vault_secret_id = dependency.secrets.outputs.zitadel_master_key_secret_id },
    { name = "zitadel-db-password", key_vault_secret_id = dependency.secrets.outputs.postgres_admin_password_secret_id },
    { name = "zitadel-admin-password", key_vault_secret_id = dependency.secrets.outputs.zitadel_admin_password_secret_id },
  ]

  # Secret-sourced env vars. Master key is passed via env (not the --masterkey
  # arg) to avoid shell-quoting issues with special characters — requires the
  # --masterkeyFromEnv flag in args above for Zitadel to actually read it.
  secret_env_vars = {
    "ZITADEL_MASTERKEY"                        = "zitadel-master-key"
    "ZITADEL_DATABASE_POSTGRES_USER_PASSWORD"  = "zitadel-db-password"
    "ZITADEL_DATABASE_POSTGRES_ADMIN_PASSWORD" = "zitadel-db-password"
    "ZITADEL_FIRSTINSTANCE_ORG_HUMAN_PASSWORD" = "zitadel-admin-password"
  }

  env_vars = {
    # --- Database (shared Postgres Flexible Server, dedicated `zitadel` DB) ---
    "ZITADEL_DATABASE_POSTGRES_HOST"           = dependency.postgresql.outputs.fqdn
    "ZITADEL_DATABASE_POSTGRES_PORT"           = "5432"
    "ZITADEL_DATABASE_POSTGRES_DATABASE"       = "zitadel"
    "ZITADEL_DATABASE_POSTGRES_USER_USERNAME"  = "pgadmin"
    "ZITADEL_DATABASE_POSTGRES_USER_SSL_MODE"  = "require"
    "ZITADEL_DATABASE_POSTGRES_ADMIN_USERNAME" = "pgadmin"
    "ZITADEL_DATABASE_POSTGRES_ADMIN_SSL_MODE" = "require"

    # --- External addressing (issuer/discovery host) ---
    # ACA assigns an external app the FQDN "<name>.<env-default-domain>" (internal
    # apps get "<name>.internal.<...>"), predictable before the app exists — so
    # EXTERNALDOMAIN is set without a self-referential output. It MUST equal the
    # browser-facing host or OIDC discovery + JWT issuer validation break.
    "ZITADEL_EXTERNALSECURE" = "true"
    "ZITADEL_EXTERNALDOMAIN" = "${local.name}.${dependency.environment.outputs.default_domain}"
    "ZITADEL_EXTERNALPORT"   = "443"

    # --- First-instance bootstrap: human admin (login works on first boot) ---
    "ZITADEL_FIRSTINSTANCE_ORG_HUMAN_USERNAME"               = "admin"
    "ZITADEL_FIRSTINSTANCE_ORG_HUMAN_PASSWORDCHANGEREQUIRED" = "false"
    "ZITADEL_FIRSTINSTANCE_ORG_HUMAN_EMAIL_ADDRESS"          = "admin@signalbeam.local"
    "ZITADEL_FIRSTINSTANCE_ORG_HUMAN_EMAIL_VERIFIED"         = "true"
    "ZITADEL_FIRSTINSTANCE_ORG_HUMAN_FIRSTNAME"              = "SignalBeam"
    "ZITADEL_FIRSTINSTANCE_ORG_HUMAN_LASTNAME"               = "Admin"
  }
}
