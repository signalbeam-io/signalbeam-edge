variable "key_vault_id" {
  description = "Key Vault ID where secrets are stored"
  type        = string
}

variable "postgres_fqdn" {
  description = "Postgres Flexible Server FQDN"
  type        = string
}

variable "database_name" {
  description = "Database name for the connection string"
  type        = string
  default     = "signalbeam"
}

variable "administrator_login" {
  description = "Postgres administrator login"
  type        = string
  default     = "pgadmin"
}

variable "administrator_password" {
  description = "Postgres administrator password"
  type        = string
  sensitive   = true
}

variable "tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default     = {}
}
