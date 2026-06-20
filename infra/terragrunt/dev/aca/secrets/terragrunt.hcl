include "root" {
  path = find_in_parent_folders()
}

terraform {
  source = "../../../../terraform/modules/app-secrets"
}

dependency "key_vault" {
  config_path = "../../key-vault"

  mock_outputs = {
    id                        = "/subscriptions/00000000/resourceGroups/mock-rg/providers/Microsoft.KeyVault/vaults/mock-kv"
    name                      = "mock-kv"
    vault_uri                 = "https://mock-kv.vault.azure.net/"
    postgresql_admin_password = "mock-password"
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

inputs = {
  key_vault_id           = dependency.key_vault.outputs.id
  key_vault_uri          = dependency.key_vault.outputs.vault_uri
  postgres_fqdn          = dependency.postgresql.outputs.fqdn
  administrator_password = dependency.key_vault.outputs.postgresql_admin_password
}
