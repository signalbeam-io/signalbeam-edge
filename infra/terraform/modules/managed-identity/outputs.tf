output "id" {
  description = "User-assigned managed identity ID"
  value       = azurerm_user_assigned_identity.workload.id
}

output "principal_id" {
  description = "User-assigned managed identity principal (object) ID"
  value       = azurerm_user_assigned_identity.workload.principal_id
}

output "client_id" {
  description = "User-assigned managed identity client ID"
  value       = azurerm_user_assigned_identity.workload.client_id
}

output "name" {
  description = "User-assigned managed identity name"
  value       = azurerm_user_assigned_identity.workload.name
}
