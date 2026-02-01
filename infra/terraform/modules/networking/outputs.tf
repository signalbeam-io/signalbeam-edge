output "vnet_id" {
  description = "Virtual network ID"
  value       = azurerm_virtual_network.this.id
}

output "vnet_name" {
  description = "Virtual network name"
  value       = azurerm_virtual_network.this.name
}

output "aks_subnet_id" {
  description = "AKS subnet ID"
  value       = azurerm_subnet.aks.id
}

output "postgresql_subnet_id" {
  description = "PostgreSQL delegated subnet ID"
  value       = azurerm_subnet.postgresql.id
}

output "services_subnet_id" {
  description = "Services subnet ID"
  value       = azurerm_subnet.services.id
}

output "postgresql_private_dns_zone_id" {
  description = "PostgreSQL private DNS zone ID"
  value       = azurerm_private_dns_zone.postgresql.id
}
