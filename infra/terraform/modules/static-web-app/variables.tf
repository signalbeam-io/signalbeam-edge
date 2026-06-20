variable "environment" {
  description = "Environment name (e.g. dev, staging, prod)"
  type        = string
}

variable "location" {
  description = "Azure region for the Static Web App. SWA is not available in every region (e.g. not northeurope) — defaults to West Europe."
  type        = string
  default     = "westeurope"
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

variable "sku_tier" {
  description = "Static Web App SKU tier"
  type        = string
  default     = "Free"
}

variable "sku_size" {
  description = "Static Web App SKU size"
  type        = string
  default     = "Free"
}

variable "tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default     = {}
}
