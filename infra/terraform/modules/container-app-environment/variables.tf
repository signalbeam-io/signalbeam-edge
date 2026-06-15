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

variable "log_analytics_workspace_id" {
  description = "Log Analytics workspace ID for container app logs"
  type        = string
}

variable "infrastructure_subnet_id" {
  description = "Delegated subnet ID for the Container Apps environment (Microsoft.App/environments)"
  type        = string
}

variable "aca_subnet_id" {
  description = "ACA subnet ID, allow-listed on the NATS Azure Files storage account (usually the same as infrastructure_subnet_id)"
  type        = string
}

variable "internal_load_balancer_enabled" {
  description = "If true, the environment is internal-only (no public ingress). Keep false so the ApiGateway can be reached at a public FQDN."
  type        = bool
  default     = false
}

variable "nats_share_quota_gb" {
  description = "Quota in GB for the NATS JetStream Azure Files share"
  type        = number
  default     = 5
}

variable "tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default     = {}
}
