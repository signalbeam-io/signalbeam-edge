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
  description = "Kubernetes namespace to deploy NATS into"
  type        = string
  default     = "signalbeam"
}

variable "chart_version" {
  description = "NATS Helm chart version"
  type        = string
  default     = "1.2.12"
}

variable "cluster_replicas" {
  description = "Number of NATS server replicas"
  type        = number
  default     = 3
}

variable "jetstream_storage_size" {
  description = "JetStream file storage size"
  type        = string
  default     = "10Gi"
}

variable "jetstream_memory_storage_size" {
  description = "JetStream in-memory storage size"
  type        = string
  default     = "1Gi"
}

variable "resources_requests_cpu" {
  description = "CPU request for each NATS pod"
  type        = string
  default     = "100m"
}

variable "resources_requests_memory" {
  description = "Memory request for each NATS pod"
  type        = string
  default     = "256Mi"
}

variable "resources_limits_cpu" {
  description = "CPU limit for each NATS pod"
  type        = string
  default     = "500m"
}

variable "resources_limits_memory" {
  description = "Memory limit for each NATS pod"
  type        = string
  default     = "512Mi"
}

variable "prometheus_enabled" {
  description = "Enable Prometheus metrics exporter"
  type        = bool
  default     = true
}

variable "tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default     = {}
}
