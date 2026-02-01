# Root Terragrunt configuration for SignalBeam Edge infrastructure.
# All child modules inherit this config.

locals {
  env_vars = read_terragrunt_config(find_in_parent_folders("env.hcl"))

  environment     = local.env_vars.locals.environment
  location        = local.env_vars.locals.location
  subscription_id = local.env_vars.locals.subscription_id
  project         = local.env_vars.locals.project

  common_tags = {
    environment = local.environment
    project     = local.project
    managed-by  = "terragrunt"
  }
}

# Remote state in Azure Storage (created by bootstrap.sh)
remote_state {
  backend = "azurerm"
  config = {
    resource_group_name  = "${local.project}-tfstate-${local.environment}-weu"
    storage_account_name = "${local.project}tfstate${local.environment}weu"
    container_name       = "tfstate"
    key                  = "${path_relative_to_include()}/terraform.tfstate"
    subscription_id      = local.subscription_id
  }
  generate = {
    path      = "backend.tf"
    if_exists = "overwrite_terragrunt"
  }
}

# Generate provider configuration
generate "provider" {
  path      = "provider.tf"
  if_exists = "overwrite_terragrunt"
  contents  = <<-EOF
    terraform {
      required_version = ">= 1.5"

      required_providers {
        azurerm = {
          source  = "hashicorp/azurerm"
          version = "~> 4.0"
        }
        random = {
          source  = "hashicorp/random"
          version = "~> 3.6"
        }
      }
    }

    provider "azurerm" {
      subscription_id = "${local.subscription_id}"
      features {
        key_vault {
          purge_soft_delete_on_destroy = true
        }
      }
    }
  EOF
}

# Generate common variables file so modules can receive standard inputs
generate "common_variables" {
  path      = "common_variables.tf"
  if_exists = "overwrite_terragrunt"
  contents  = <<-EOF
    variable "subscription_id" {
      description = "Azure subscription ID"
      type        = string
    }
  EOF
}

# Inputs passed to every module
inputs = {
  subscription_id = local.subscription_id
  environment     = local.environment
  location        = local.location
  project         = local.project
  tags            = local.common_tags
}
