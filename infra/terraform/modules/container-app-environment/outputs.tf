output "id" {
  description = "Container Apps environment ID"
  value       = azurerm_container_app_environment.this.id
}

output "name" {
  description = "Container Apps environment name"
  value       = azurerm_container_app_environment.this.name
}

output "default_domain" {
  description = "Default domain of the environment (used to build internal app FQDNs)"
  value       = azurerm_container_app_environment.this.default_domain
}

output "static_ip_address" {
  description = "Static public IP of the environment's ingress"
  value       = azurerm_container_app_environment.this.static_ip_address
}

output "nats_storage_name" {
  description = "Name of the registered Azure Files storage for NATS (mount by this name)"
  value       = azurerm_container_app_environment_storage.nats.name
}

output "files_storage_account_name" {
  description = "Name of the storage account backing ACA Azure Files"
  value       = azurerm_storage_account.files.name
}
