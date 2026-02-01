variable "environment" {
  description = "Environment name (e.g. dev, staging, prod)"
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

variable "domain_name" {
  description = "DNS zone domain name"
  type        = string
  default     = "dev.signalbeam.io"
}

variable "workload_identity_principal_id" {
  description = "Principal ID of the workload managed identity (for cert-manager DNS validation)"
  type        = string
}

variable "tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default     = {}
}
