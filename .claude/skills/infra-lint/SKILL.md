---
name: infra-lint
description: Lint Terraform, Terragrunt, and Helm charts only — skips .NET and frontend. Use for infrastructure-only changes when /lint would be overkill.
allowed-tools: Bash
user-invocable: true
---

# Infrastructure Lint

Fast lint for infrastructure files only. Skips .NET and frontend checks.

## Arguments

- `--fix` or `fix` — Apply auto-fixes (formatting only)
- `terraform` — Only lint Terraform/Terragrunt
- `helm` — Only lint Helm charts

If no argument, lint both Terraform and Helm.

## Terraform / Terragrunt

**Format check:**
```bash
terraform fmt -check -recursive infra
```

**Format fix (if `--fix`):**
```bash
terraform fmt -recursive infra
```

**Validate modules:**
```bash
for dir in $(find infra/terraform/modules -name "*.tf" -exec dirname {} \; | sort -u); do
  echo "=== Validating $dir ==="
  terraform -chdir="$dir" init -backend=false -input=false 2>/dev/null
  terraform -chdir="$dir" validate
done
```

**Terragrunt validate:**
```bash
find infra -name "terragrunt.hcl" -execdir terragrunt validate \;
```

## Helm Charts

**Lint all charts:**
```bash
for chart in deploy/charts/*/; do
  echo "=== Linting $chart ==="
  helm lint "$chart"
  helm template test "$chart" > /dev/null
done
```

If `deploy/charts/` doesn't exist, skip Helm linting and note it.

## Output

Report pass/fail per category:
```
- Terraform Format: PASS/FAIL
- Terraform Validate: PASS/FAIL
- Terragrunt Validate: PASS/FAIL
- Helm Lint: PASS/FAIL (or SKIPPED)
- Helm Template: PASS/FAIL (or SKIPPED)
```
