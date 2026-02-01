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
  description = "AKS subnet ID for storage firewall"
  type        = string
}

variable "workload_identity_principal_id" {
  description = "Principal ID of the workload managed identity"
  type        = string
}

variable "containers" {
  description = "List of blob container names to create"
  type        = list(string)
  default     = ["signalbeam-bundles", "device-bundles"]
}

variable "tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default     = {}
}
