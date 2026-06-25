include "root" {
  path = find_in_parent_folders()
}

locals {
  env  = read_terragrunt_config(find_in_parent_folders("env.hcl")).locals
  name = "${local.env.project}-caj-zitadel-setup-${local.env.environment}"
}

terraform {
  source = "../../../../terraform/modules/container-app-job"
}

dependency "resource_group" {
  config_path = "../../resource-group"

  mock_outputs = {
    name     = "mock-rg"
    id       = "/subscriptions/00000000/resourceGroups/mock-rg"
    location = "northeurope"
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
    default_domain    = "mockenv.northeurope.azurecontainerapps.io"
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

# Init job. Declaring it orders the `run --all` DAG so the eventstore-creating
# `zitadel init` job is created before this setup job. init must also be EXECUTED
# to completion before setup is run (see this unit's args comment) — `setup`
# fails with "relation eventstore.events does not exist" otherwise.
dependency "zitadel_init" {
  config_path = "../zitadel-init"

  mock_outputs = {
    id   = "/subscriptions/00000000/resourceGroups/mock-rg/providers/Microsoft.App/jobs/mock-caj-init"
    name = "mock-caj-zitadel-init"
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
}

inputs = {
  name                         = local.name
  location                     = local.env.location
  resource_group_name          = dependency.resource_group.outputs.name
  container_app_environment_id = dependency.environment.outputs.id
  managed_identity_id          = dependency.managed_identity.outputs.id

  # Same pinned image as the zitadel service so the schema the job builds matches
  # exactly what `zitadel start` expects.
  image = "ghcr.io/zitadel/zitadel:v2.66.3"

  # `setup` runs DB migrations AND creates the first instance, then exits. It
  # REQUIRES the `zitadel-init` job to have run first (init creates the eventstore
  # schema + base tables setup migrates); without it setup dies with "relation
  # eventstore.events does not exist". This is the ONLY place migrations run — the
  # zitadel service runs `start` with no migrations, so overlapping service
  # revisions during a rollout can never race the 03_default_instance migration.
  # Bootstrap order: run zitadel-init to completion, then run this job, then
  # (re)start the service. It is idempotent — re-running on a migrated DB is a
  # no-op. --masterkeyFromEnv reads the 32-char master key from ZITADEL_MASTERKEY.
  args = ["setup", "--masterkeyFromEnv"]

  cpu    = 0.5
  memory = "1.0Gi"

  kv_secrets = [
    { name = "zitadel-master-key", key_vault_secret_id = dependency.secrets.outputs.zitadel_master_key_secret_id },
    { name = "zitadel-db-password", key_vault_secret_id = dependency.secrets.outputs.postgres_admin_password_secret_id },
    { name = "zitadel-admin-password", key_vault_secret_id = dependency.secrets.outputs.zitadel_admin_password_secret_id },
  ]

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

    # --- External addressing — must match the service so the issuer the first
    # instance is created with equals the browser-facing host. ---
    "ZITADEL_EXTERNALSECURE" = "true"
    "ZITADEL_EXTERNALDOMAIN" = "${local.env.project}-ca-zitadel-${local.env.environment}.${dependency.environment.outputs.default_domain}"
    "ZITADEL_EXTERNALPORT"   = "443"

    # --- Machine ID (sonyflake) identification ---
    # `setup` also generates sonyflake IDs (first instance/org/user), so it hits
    # the same machine-ID panic as the service on ACA. Use hostname identification
    # and disable the Private-IP + GCP-webhook methods that fail here. Must match
    # the service's setting (see the zitadel unit for the full rationale).
    "ZITADEL_MACHINE_IDENTIFICATION_HOSTNAME_ENABLED"  = "true"
    "ZITADEL_MACHINE_IDENTIFICATION_PRIVATEIP_ENABLED" = "false"
    "ZITADEL_MACHINE_IDENTIFICATION_WEBHOOK_ENABLED"   = "false"

    # --- First-instance bootstrap: human admin (created during setup) ---
    "ZITADEL_FIRSTINSTANCE_ORG_HUMAN_USERNAME"               = "admin"
    "ZITADEL_FIRSTINSTANCE_ORG_HUMAN_PASSWORDCHANGEREQUIRED" = "false"
    "ZITADEL_FIRSTINSTANCE_ORG_HUMAN_EMAIL_ADDRESS"          = "admin@signalbeam.local"
    "ZITADEL_FIRSTINSTANCE_ORG_HUMAN_EMAIL_VERIFIED"         = "true"
    "ZITADEL_FIRSTINSTANCE_ORG_HUMAN_FIRSTNAME"              = "SignalBeam"
    "ZITADEL_FIRSTINSTANCE_ORG_HUMAN_LASTNAME"               = "Admin"
  }
}
