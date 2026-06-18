variable "environment" {
  description = "Environment name (e.g. dev, staging, prod)"
  type        = string
}

variable "location" {
  description = "Azure region"
  type        = string
}

variable "project" {
  description = "Project prefix for resource naming"
  type        = string
  default     = "sb"
}

variable "resource_group_name" {
  description = "Name of the resource group"
  type        = string
}

variable "aks_subnet_id" {
  description = "AKS subnet ID for Key Vault network ACLs"
  type        = string
}

variable "services_subnet_id" {
  description = "Services subnet ID for Key Vault network ACLs"
  type        = string
}

variable "aca_subnet_id" {
  description = "Azure Container Apps subnet ID for Key Vault network ACLs (empty to omit)"
  type        = string
  default     = ""
}

variable "workload_identity_principal_id" {
  description = "Principal ID of the workload managed identity"
  type        = string
}

variable "soft_delete_retention_days" {
  description = "Number of days for soft delete retention"
  type        = number
  default     = 7
}

variable "allowed_ip_addresses" {
  description = "Public IPs/CIDRs allowed through the Key Vault firewall for data-plane access (e.g. the operator's IP so Terraform can write secrets). Empty list keeps the vault subnet-only."
  type        = list(string)
  default     = []
}

variable "tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default     = {}
}
