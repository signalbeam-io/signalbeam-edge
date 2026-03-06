---
name: infra-apply
description: Run terraform plan for review and optionally apply infrastructure changes. Use when the user wants to preview or deploy infrastructure — always shows the plan before any apply.
allowed-tools: Bash, Read, Glob
user-invocable: true
---

# Infrastructure Apply

Generate a Terraform plan for review. Optionally apply after user confirmation.

## Arguments

- `{environment}` — Target environment (e.g., `dev`, `staging`, `prod`). Default: `dev`
- `{module}` — Specific module to plan (e.g., `aks-cluster`, `postgresql`). If omitted, plan all.
- `--apply` — Apply after showing the plan (requires explicit user confirmation)

## Process

### Step 1: Validate First

Run `/infra-lint terraform` to ensure all modules are valid before planning.

### Step 2: Generate Plan

**Single module:**
```bash
cd infra/terragrunt/{environment}/{module}
terragrunt plan -out=tfplan
```

**All modules:**
```bash
cd infra/terragrunt/{environment}
terragrunt run --all plan
```

### Step 3: Review Plan Output

Display the plan summary:
- Resources to add
- Resources to change
- Resources to destroy

**CRITICAL:** If any resources will be **destroyed**, highlight this prominently and require explicit user confirmation before proceeding.

### Step 4: Apply (only if `--apply` and user confirms)

**Single module:**
```bash
cd infra/terragrunt/{environment}/{module}
terragrunt apply tfplan
```

**All modules:**
```bash
cd infra/terragrunt/{environment}
terragrunt run --all apply
```

## Safety Rules

- NEVER apply to `prod` without showing the plan first and getting explicit user confirmation
- ALWAYS run `terragrunt plan` before `terragrunt apply`
- If the plan shows unexpected destroys, STOP and ask the user
- Clean up plan files after apply: `rm -f tfplan`
- For `--all` operations, respect the dependency order defined in Terragrunt configs
