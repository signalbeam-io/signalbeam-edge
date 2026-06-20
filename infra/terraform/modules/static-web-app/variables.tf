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
  description = "Static Web App SKU tier (must match sku_size)"
  type        = string
  default     = "Free"

  validation {
    condition     = contains(["Free", "Standard"], var.sku_tier)
    error_message = "sku_tier must be \"Free\" or \"Standard\"."
  }
}

variable "sku_size" {
  description = "Static Web App SKU size (must match sku_tier)"
  type        = string
  default     = "Free"

  validation {
    condition     = contains(["Free", "Standard"], var.sku_size)
    error_message = "sku_size must be \"Free\" or \"Standard\"."
  }
}

variable "tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default     = {}
}
