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

variable "vnet_address_space" {
  description = "Address space for the virtual network"
  type        = list(string)
  default     = ["10.0.0.0/16"]
}

variable "aks_subnet_prefix" {
  description = "Address prefix for AKS subnet"
  type        = string
  default     = "10.0.0.0/20"
}

variable "postgresql_subnet_prefix" {
  description = "Address prefix for PostgreSQL delegated subnet"
  type        = string
  default     = "10.0.16.0/24"
}

variable "services_subnet_prefix" {
  description = "Address prefix for services subnet"
  type        = string
  default     = "10.0.17.0/24"
}

variable "aca_subnet_prefix" {
  description = "Address prefix for the Azure Container Apps delegated subnet (Consumption requires at least /27)"
  type        = string
  default     = "10.0.18.0/27"
}

variable "tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default     = {}
}
