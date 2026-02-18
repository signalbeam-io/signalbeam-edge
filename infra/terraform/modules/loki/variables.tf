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

variable "namespace" {
  description = "Kubernetes namespace for Loki"
  type        = string
  default     = "monitoring"
}

variable "chart_version" {
  description = "Loki Helm chart version"
  type        = string
  default     = "6.24.0"
}

variable "storage_size" {
  description = "Loki persistent volume size"
  type        = string
  default     = "10Gi"
}

variable "retention_period" {
  description = "Log retention period"
  type        = string
  default     = "168h"
}

variable "tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default     = {}
}
