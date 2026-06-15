output "id" {
  description = "Container app ID"
  value       = azurerm_container_app.this.id
}

output "name" {
  description = "Container app name"
  value       = azurerm_container_app.this.name
}

output "fqdn" {
  description = "Ingress FQDN (public for external apps, internal VNet FQDN otherwise); null if ingress disabled"
  value       = try(azurerm_container_app.this.ingress[0].fqdn, null)
}

output "latest_revision_name" {
  description = "Name of the latest revision"
  value       = azurerm_container_app.this.latest_revision_name
}
