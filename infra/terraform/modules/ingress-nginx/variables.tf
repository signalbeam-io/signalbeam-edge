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
  description = "ingress-nginx Helm chart version"
  type        = string
  default     = "4.12.0"
}

variable "replica_count" {
  description = "Number of ingress controller replicas"
  type        = number
  default     = 2
}

variable "tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default     = {}
}
