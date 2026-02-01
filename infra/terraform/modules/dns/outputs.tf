output "id" {
  description = "DNS zone ID"
  value       = azurerm_dns_zone.this.id
}

output "name" {
  description = "DNS zone name"
  value       = azurerm_dns_zone.this.name
}

output "name_servers" {
  description = "DNS zone name servers (point your domain registrar NS records here)"
  value       = azurerm_dns_zone.this.name_servers
}
