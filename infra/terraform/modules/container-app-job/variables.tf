variable "name" {
  description = "Container app job name (lowercase alphanumeric and hyphens, e.g. sb-caj-zitadel-setup-dev)"
  type        = string
}

variable "resource_group_name" {
  description = "Name of the resource group"
  type        = string
}

variable "location" {
  description = "Azure region (Container App Jobs require an explicit location, unlike Container Apps)"
  type        = string
}

variable "container_app_environment_id" {
  description = "ID of the Container Apps environment"
  type        = string
}

variable "managed_identity_id" {
  description = "User-assigned managed identity ID (used for Key Vault references and registry auth)"
  type        = string
}

variable "image" {
  description = "Fully qualified container image, e.g. ghcr.io/zitadel/zitadel:v2.66.3"
  type        = string
}

variable "cpu" {
  description = "vCPU allocation for the container"
  type        = number
  default     = 0.5
}

variable "memory" {
  description = "Memory allocation for the container (e.g. 1.0Gi)"
  type        = string
  default     = "1.0Gi"
}

variable "command" {
  description = "Container entrypoint/command override (replaces the image ENTRYPOINT); empty uses the image default"
  type        = list(string)
  default     = []
}

variable "args" {
  description = "Container args appended to the image ENTRYPOINT (like Docker CMD), e.g. Zitadel's `setup`; empty uses the image default"
  type        = list(string)
  default     = []
}

variable "workload_profile_name" {
  description = "Workload profile name on the environment"
  type        = string
  default     = "Consumption"
}

variable "replica_timeout_in_seconds" {
  description = "Maximum time a job replica may run before it is terminated"
  type        = number
  default     = 1800
}

variable "replica_retry_limit" {
  description = "Number of times to retry a failed replica before the execution fails"
  type        = number
  default     = 1
}

# --- Environment & secrets ---

variable "env_vars" {
  description = "Plain (non-secret) environment variables"
  type        = map(string)
  default     = {}
}

variable "secret_env_vars" {
  description = "Environment variables sourced from secrets: map of ENV_NAME => secret_name"
  type        = map(string)
  default     = {}
}

variable "kv_secrets" {
  description = "Key Vault reference secrets: list of { name, key_vault_secret_id }"
  type = list(object({
    name                = string
    key_vault_secret_id = string
  }))
  default = []
}

# --- Private registry (GHCR) ---

variable "registry_server" {
  description = "Container registry server (empty to skip, e.g. ghcr.io)"
  type        = string
  default     = ""
}

variable "registry_username" {
  description = "Container registry username"
  type        = string
  default     = ""
}

variable "registry_password_secret_name" {
  description = "Name of the secret (in kv_secrets) holding the registry password/PAT"
  type        = string
  default     = ""
}

variable "tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default     = {}
}
