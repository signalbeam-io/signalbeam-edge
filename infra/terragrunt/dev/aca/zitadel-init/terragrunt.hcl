include "root" {
  path = find_in_parent_folders()
}

locals {
  env  = read_terragrunt_config(find_in_parent_folders("env.hcl")).locals
  name = "${local.env.project}-caj-zitadel-init-${local.env.environment}"
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

inputs = {
  name                         = local.name
  location                     = local.env.location
  resource_group_name          = dependency.resource_group.outputs.name
  container_app_environment_id = dependency.environment.outputs.id
  managed_identity_id          = dependency.managed_identity.outputs.id

  # Same pinned image as the setup job + service.
  image = "ghcr.io/zitadel/zitadel:v2.66.3"

  # `init` creates the eventstore schema, the base `eventstore.events`/`events2`
  # tables, and the application DB role — the foundation that `zitadel setup`
  # (migrations) and `zitadel start` both REQUIRE. Zitadel's own commands are
  # layered: init -> setup -> start (the old `start-from-init` did all three in
  # one process). Because the image is distroless, the three phases can't be
  # chained in one container, so init is its own one-shot job that must run to
  # completion BEFORE the setup job:
  #   az containerapp job start --name <this job> -g <rg>   # wait for Succeeded
  # It is idempotent — re-running against an initialized DB is a no-op.
  args = ["init"]

  cpu    = 0.5
  memory = "1.0Gi"

  # init connects as the Postgres admin to create the schema + the application
  # role (whose password it sets from the user secret).
  kv_secrets = [
    { name = "zitadel-db-password", key_vault_secret_id = dependency.secrets.outputs.postgres_admin_password_secret_id },
  ]

  secret_env_vars = {
    "ZITADEL_DATABASE_POSTGRES_USER_PASSWORD"  = "zitadel-db-password"
    "ZITADEL_DATABASE_POSTGRES_ADMIN_PASSWORD" = "zitadel-db-password"
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
  }
}
