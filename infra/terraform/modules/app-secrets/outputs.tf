# Versionless secret IDs — used as ACA Key Vault references so rotation does not
# require redeploying the container apps.

output "db_connection_secret_id" {
  description = "Versionless Key Vault secret ID for the Postgres connection string"
  value       = azurerm_key_vault_secret.db_connection.versionless_id
}

output "ghcr_pat_secret_id" {
  description = "Versionless Key Vault secret ID for the GHCR PAT"
  value       = azurerm_key_vault_secret.ghcr_pat.versionless_id
}

# Zitadel secrets. The master key and Postgres admin password are created by the
# key-vault module; their versionless IDs are built from the vault URI so the
# Zitadel container app can reference them without a cross-module data source.
output "zitadel_master_key_secret_id" {
  description = "Versionless Key Vault secret ID for the Zitadel master key"
  value       = "${var.key_vault_uri}secrets/zitadel-master-key"
}

output "postgres_admin_password_secret_id" {
  description = "Versionless Key Vault secret ID for the Postgres admin password (reused as Zitadel's DB password)"
  value       = "${var.key_vault_uri}secrets/postgresql-admin-password"
}

output "zitadel_admin_password_secret_id" {
  description = "Versionless Key Vault secret ID for the Zitadel first-instance admin password"
  value       = azurerm_key_vault_secret.zitadel_admin_password.versionless_id
}
