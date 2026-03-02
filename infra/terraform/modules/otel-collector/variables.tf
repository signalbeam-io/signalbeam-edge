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
  description = "Kubernetes namespace for the OTEL Collector"
  type        = string
  default     = "signalbeam"
}

variable "chart_version" {
  description = "OpenTelemetry Collector Helm chart version"
  type        = string
  default     = "0.108.0"
}

variable "monitoring_namespace" {
  description = "Namespace where Prometheus, Loki, and Tempo are deployed"
  type        = string
  default     = "monitoring"
}

variable "tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default     = {}
}
