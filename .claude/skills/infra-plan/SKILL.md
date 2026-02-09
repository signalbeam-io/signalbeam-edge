---
name: infra-plan
description: Plan infrastructure changes — Terraform modules, Helm charts, CI/CD pipelines
allowed-tools: Read, Glob, Grep, Task
user-invocable: true
---

# Infrastructure Plan

Break down an infrastructure change into implementation tasks. This is the infra equivalent of `/plan-feature`.

## Arguments

- `{description}` — What infrastructure change is needed (required)

## Process

1. **Understand the request** — Parse what the user wants to change (new module, modify existing, add chart, change CI/CD)

2. **Analyze current state** — Read relevant files:
   - `infra/terraform/modules/` — existing Terraform modules
   - `infra/terragrunt/` — environment configurations and dependencies
   - `deploy/charts/` — existing Helm charts
   - `.github/workflows/` — CI/CD pipelines

3. **Identify scope** — Categorize the change:
   - **Terraform:** New module, modify module, add environment, change dependencies
   - **Helm:** New chart, modify templates, add values overlay
   - **CI/CD:** New workflow, modify pipeline, add environment gate
   - **Cross-cutting:** Changes spanning multiple categories

4. **Map dependencies** — Terraform modules have ordering constraints via Terragrunt dependencies. Identify:
   - Which modules depend on this change
   - Which modules this change depends on
   - Whether new Terragrunt wiring is needed

5. **Create plan** — Output a structured plan:

```markdown
## Infrastructure Change Plan

### Summary
{one-line description}

### Category
Terraform | Helm | CI/CD | Cross-cutting

### Changes Required

#### {Category 1}
1. {task} — {file or directory affected}
2. {task} — {file or directory affected}

#### {Category 2} (if cross-cutting)
1. {task} — {file or directory affected}

### Dependency Order
{execution order considering Terragrunt dependencies}

### Validation Steps
- [ ] `terraform validate` passes for affected modules
- [ ] `terraform fmt -check` passes
- [ ] `terragrunt validate` passes
- [ ] `helm lint` passes (if charts changed)
- [ ] `helm template` renders without errors (if charts changed)

### Risk Assessment
- Blast radius: {low/medium/high}
- Reversibility: {easy/hard}
- Requires `terraform plan` review: {yes/no}
```

## Guidelines

- Always check Terragrunt dependency graph before suggesting module changes
- Flag any changes that affect production environments
- Prefer modifying existing modules over creating new ones when the change is small
- For new Azure resources, check if an existing module can be extended first
