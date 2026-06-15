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
