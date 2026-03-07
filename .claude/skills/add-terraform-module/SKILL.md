---
name: add-terraform-module
description: Scaffold a new Terraform module with variables, main, and outputs. Use when provisioning a new Azure resource (database, cache, storage, etc.) that needs its own Terraform module and Terragrunt wiring.
allowed-tools: Write, Read, Glob, Grep
user-invocable: true
---

# Add Terraform Module

Scaffold a new Terraform module following project conventions.

## Arguments

- `{module-name}` — Name of the module (required, e.g., `redis`, `service-bus`)
- `{description}` — What this module provisions (optional)

## Process

1. Read existing modules in `infra/terraform/modules/` to follow established patterns.

2. Create the module directory:

```
infra/terraform/modules/{module-name}/
├── variables.tf
├── main.tf
├── outputs.tf
```

3. **variables.tf** — Always include these standard variables:
```hcl
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

variable "tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default     = {}
}
```

Add module-specific variables after the standard ones.

4. **main.tf** — Follow the naming convention from existing modules:
```hcl
locals {
  location_short = "weu"
  name           = "${var.project}-{resource-abbrev}-${var.environment}-${local.location_short}"
}
```

5. **outputs.tf** — Export `name`, `id`, and any connection strings or keys needed by downstream modules.

6. Create Terragrunt wiring for dev environment:

```
infra/terragrunt/dev/{module-name}/
└── terragrunt.hcl
```

With content:
```hcl
include "root" {
  path = find_in_parent_folders()
}

terraform {
  source = "../../../terraform/modules/{module-name}"
}

dependency "resource_group" {
  config_path = "../resource-group"
}

inputs = {
  resource_group_name = dependency.resource_group.outputs.name
}
```

Add additional dependencies based on what the module needs.

## Output

After scaffolding, report:
```
## Scaffolded: {module-name} Terraform module

Files created:
- `infra/terraform/modules/{module-name}/variables.tf`
- `infra/terraform/modules/{module-name}/main.tf`
- `infra/terraform/modules/{module-name}/outputs.tf`
- `infra/terragrunt/dev/{module-name}/terragrunt.hcl`

Next: Add resource definitions in `main.tf`, then run `/infra-lint` to validate.
```

## Error Handling

- **Module directory already exists:** Warn the user and ask whether to overwrite or extend.
- **Terraform not installed:** Suggest installing with `brew install terraform`.
- **Terragrunt root config not found:** Check that `infra/terragrunt/terragrunt.hcl` exists as the root config.

## After Scaffolding

Remind to:
- Add module-specific resource definitions in `main.tf`
- Add any needed dependencies in `terragrunt.hcl`
- Run `/infra-lint` to validate
