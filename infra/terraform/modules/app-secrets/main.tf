# Application secrets stored in Key Vault for the ACA control plane.
#
# These are consumed by the container apps as Key Vault *references* (resolved at
# runtime by the workload managed identity), so the values are never embedded in
# the container app resource definitions.

# Postgres connection string for the shared "signalbeam" database. Azure Flexible
# Server enforces TLS (require_secure_transport=on); Trust Server Certificate is
# acceptable for in-VNet dogfood traffic.
resource "azurerm_key_vault_secret" "db_connection" {
  name = "db-connection-signalbeam"
  value = format(
    "Host=%s;Port=5432;Database=%s;Username=%s;Password=%s;Ssl Mode=Require;Trust Server Certificate=true",
    var.postgres_fqdn,
    var.database_name,
    var.administrator_login,
    var.administrator_password,
  )
  key_vault_id = var.key_vault_id
  tags         = var.tags
}

# GHCR personal access token (read:packages). Created as a placeholder here so the
# secret and ACA Key Vault reference can be provisioned; the real PAT is set
# out-of-band so it never enters Terraform state:
#   az keyvault secret set --vault-name <kv> --name ghcr-pat --value <PAT>
resource "azurerm_key_vault_secret" "ghcr_pat" {
  name         = "ghcr-pat"
  value        = "REPLACE_VIA_AZ_CLI"
  key_vault_id = var.key_vault_id
  tags         = var.tags

  lifecycle {
    ignore_changes = [value]
  }
}
