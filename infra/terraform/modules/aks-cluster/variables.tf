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
  description = "Subnet ID for the AKS default node pool"
  type        = string
}

variable "container_registry_id" {
  description = "ACR ID for AcrPull role assignment"
  type        = string
}

variable "log_analytics_workspace_id" {
  description = "Log Analytics workspace ID for OMS agent"
  type        = string
}

variable "workload_identity_id" {
  description = "User-assigned managed identity resource ID"
  type        = string
}

variable "workload_identity_client_id" {
  description = "User-assigned managed identity client ID"
  type        = string
}

variable "key_vault_id" {
  description = "Key Vault ID for secrets provider"
  type        = string
}

variable "vm_size" {
  description = "VM size for the default node pool"
  type        = string
  default     = "Standard_B4ms"
}

variable "min_node_count" {
  description = "Minimum node count for auto-scaling"
  type        = number
  default     = 1
}

variable "max_node_count" {
  description = "Maximum node count for auto-scaling"
  type        = number
  default     = 2
}

variable "kubernetes_version" {
  description = "Kubernetes version (null = latest stable)"
  type        = string
  default     = null
}

variable "service_cidr" {
  description = "Service CIDR for Kubernetes services (must not overlap with VNet)"
  type        = string
  default     = "10.1.0.0/16"
}

variable "dns_service_ip" {
  description = "DNS service IP (must be within service_cidr)"
  type        = string
  default     = "10.1.0.10"
}

variable "workload_sa_namespace" {
  description = "Kubernetes namespace for workload service account federated credential"
  type        = string
  default     = "signalbeam"
}

variable "workload_sa_name" {
  description = "Kubernetes service account name for workload federated credential"
  type        = string
  default     = "signalbeam-workload-sa"
}

variable "cert_manager_namespace" {
  description = "Kubernetes namespace for cert-manager federated credential"
  type        = string
  default     = "cert-manager"
}

variable "cert_manager_sa_name" {
  description = "Kubernetes service account name for cert-manager federated credential"
  type        = string
  default     = "cert-manager"
}

variable "tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default     = {}
}
