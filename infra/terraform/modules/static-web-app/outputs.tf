output "id" {
  description = "Static Web App resource ID"
  value       = azurerm_static_web_app.this.id
}

output "name" {
  description = "Static Web App name"
  value       = azurerm_static_web_app.this.name
}

output "default_host_name" {
  description = "Default *.azurestaticapps.net hostname for the dashboard"
  value       = azurerm_static_web_app.this.default_host_name
}

output "api_key" {
  description = "Deployment API token used by the GitHub Actions deploy workflow. Store as the AZURE_STATIC_WEB_APPS_API_TOKEN repo secret."
  value       = azurerm_static_web_app.this.api_key
  sensitive   = true
}
