variable "name" {
  description = "Container app name (lowercase alphanumeric and hyphens, e.g. sb-ca-devicemanager-dev)"
  type        = string
}

variable "resource_group_name" {
  description = "Name of the resource group"
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
  description = "Fully qualified container image, e.g. ghcr.io/signalbeam-io/devicemanager:latest"
  type        = string
}

variable "cpu" {
  description = "vCPU allocation for the container"
  type        = number
  default     = 0.25
}

variable "memory" {
  description = "Memory allocation for the container (e.g. 0.5Gi)"
  type        = string
  default     = "0.5Gi"
}

variable "command" {
  description = "Container entrypoint/command override (replaces the image ENTRYPOINT, e.g. NATS JetStream flags); empty uses the image default"
  type        = list(string)
  default     = []
}

variable "args" {
  description = "Container args appended to the image ENTRYPOINT (like Docker CMD). Use this — not command — when the entrypoint is a binary and you pass a subcommand (e.g. Zitadel's `start-from-init`); empty uses the image default"
  type        = list(string)
  default     = []
}

variable "min_replicas" {
  description = "Minimum replicas (0 enables scale-to-zero; set 1 for stateful apps like NATS)"
  type        = number
  default     = 0
}

variable "max_replicas" {
  description = "Maximum replicas"
  type        = number
  default     = 1
}

variable "revision_mode" {
  description = "Revision mode (Single or Multiple)"
  type        = string
  default     = "Single"
}

variable "workload_profile_name" {
  description = "Workload profile name on the environment"
  type        = string
  default     = "Consumption"
}

# --- Ingress ---

variable "ingress_enabled" {
  description = "Whether the app exposes ingress"
  type        = bool
  default     = true
}

variable "ingress_external" {
  description = "Whether ingress is public (true only for the ApiGateway); otherwise internal to the VNet"
  type        = bool
  default     = false
}

variable "target_port" {
  description = "Container port that ingress routes to"
  type        = number
  default     = 8080
}

variable "transport" {
  description = "Ingress transport: auto, http, http2, or tcp (tcp for NATS)"
  type        = string
  default     = "auto"
}

variable "allow_insecure_connections" {
  description = "Allow insecure (HTTP) ingress connections"
  type        = bool
  default     = false
}

variable "liveness_probe_path" {
  description = "HTTP path for the liveness probe (empty to disable); use /health/live for .NET services, /healthz for NATS"
  type        = string
  default     = ""
}

variable "liveness_probe_port" {
  description = "Container port for the liveness probe (0 = use target_port); set to the NATS monitoring port when ingress is TCP"
  type        = number
  default     = 0
}

variable "readiness_probe_path" {
  description = "HTTP path for the readiness probe (empty to disable); use /health/ready for .NET services"
  type        = string
  default     = ""
}

variable "tcp_probe_port" {
  description = "Container TCP port for startup + readiness probes (0 = disabled). Set to the NATS client port so ACA confirms the TCP port accepts connections and opens the internal TCP ingress listener; without a TCP-port readiness signal the internal LB may never route the TCP port."
  type        = number
  default     = 0
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

# --- Volumes ---

variable "volumes" {
  description = "Azure Files volume mounts: list of { name, storage_name, path }"
  type = list(object({
    name         = string
    storage_name = string
    path         = string
  }))
  default = []
}

variable "tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default     = {}
}
