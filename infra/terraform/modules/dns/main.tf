resource "azurerm_dns_zone" "this" {
  name                = var.domain_name
  resource_group_name = var.resource_group_name
  tags                = var.tags
}

# Workload identity gets DNS Zone Contributor (for cert-manager ACME DNS01 validation)
resource "azurerm_role_assignment" "dns_contributor" {
  scope                = azurerm_dns_zone.this.id
  role_definition_name = "DNS Zone Contributor"
  principal_id         = var.workload_identity_principal_id
}
