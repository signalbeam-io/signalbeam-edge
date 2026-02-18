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

variable "kube_config_raw" {
  description = "Raw kubeconfig for the AKS cluster"
  type        = string
  sensitive   = true
}

variable "chart_version" {
  description = "cert-manager Helm chart version"
  type        = string
  default     = "1.16.3"
}

variable "workload_identity_client_id" {
  description = "Azure workload identity client ID for DNS01 solver"
  type        = string
}

variable "dns_zone_name" {
  description = "Azure DNS zone name for ACME DNS01 validation"
  type        = string
}

variable "dns_zone_resource_group" {
  description = "Resource group containing the Azure DNS zone"
  type        = string
}

variable "acme_email" {
  description = "Email address for Let's Encrypt ACME registration"
  type        = string
  default     = "platform@signalbeam.io"
}

variable "tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default     = {}
}
