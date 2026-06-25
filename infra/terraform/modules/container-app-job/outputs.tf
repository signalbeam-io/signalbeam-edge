output "id" {
  description = "Container app job ID"
  value       = azurerm_container_app_job.this.id
}

output "name" {
  description = "Container app job name"
  value       = azurerm_container_app_job.this.name
}
