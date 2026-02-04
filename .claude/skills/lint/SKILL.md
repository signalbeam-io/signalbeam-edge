---
name: lint
description: Run all linters and formatters — dotnet format, ESLint, helm lint, and terraform validate
allowed-tools: Bash
user-invocable: true
---

# Lint

Run all project linters and report violations. Optionally fix auto-fixable issues.

## Arguments

- `--fix` or `fix` — Apply auto-fixes (default: report only)
- `backend` — Only lint C# code
- `frontend` — Only lint frontend code
- `helm` — Only lint Helm charts
- `terraform` — Only lint Terraform/Terragrunt code

If no argument is given, lint everything in report-only mode.

## Backend (.NET)

**Check formatting violations (report only):**
```bash
dotnet format /Users/marjangjuroski/Projects/signalbeam-edge/src/SignalBeam.sln --verify-no-changes --verbosity normal
```

**Fix formatting violations:**
```bash
dotnet format /Users/marjangjuroski/Projects/signalbeam-edge/src/SignalBeam.sln --verbosity normal
```

## Frontend (React/TypeScript)

**Check lint violations (report only):**
```bash
cd /Users/marjangjuroski/Projects/signalbeam-edge/web && npm run lint
```

**Fix auto-fixable violations:**
```bash
cd /Users/marjangjuroski/Projects/signalbeam-edge/web && npm run lint:fix
```

**Check TypeScript types:**
```bash
cd /Users/marjangjuroski/Projects/signalbeam-edge/web && npm run type-check
```

## Helm Charts

**Lint all charts in deploy/charts/:**
```bash
helm lint /Users/marjangjuroski/Projects/signalbeam-edge/deploy/charts/signalbeam-infrastructure
helm lint /Users/marjangjuroski/Projects/signalbeam-edge/deploy/charts/signalbeam-platform
```

If additional charts exist under `deploy/charts/`, lint those too. Use `ls /Users/marjangjuroski/Projects/signalbeam-edge/deploy/charts/` to discover all charts.

**Template render check (catches template errors without deploying):**
```bash
helm template test /Users/marjangjuroski/Projects/signalbeam-edge/deploy/charts/signalbeam-infrastructure > /dev/null
helm template test /Users/marjangjuroski/Projects/signalbeam-edge/deploy/charts/signalbeam-platform > /dev/null
```

## Terraform / Terragrunt

**Validate Terraform modules:**
```bash
# Find all Terraform module directories and validate each
for dir in $(find /Users/marjangjuroski/Projects/signalbeam-edge/infra -name "*.tf" -exec dirname {} \; | sort -u); do
  echo "=== Validating $dir ==="
  terraform -chdir="$dir" init -backend=false -input=false 2>/dev/null
  terraform -chdir="$dir" validate
done
```

**Format check (report only):**
```bash
terraform fmt -check -recursive /Users/marjangjuroski/Projects/signalbeam-edge/infra
```

**Format fix:**
```bash
terraform fmt -recursive /Users/marjangjuroski/Projects/signalbeam-edge/infra
```

**Terragrunt validate (if terragrunt.hcl files exist):**
```bash
find /Users/marjangjuroski/Projects/signalbeam-edge/infra -name "terragrunt.hcl" -execdir terragrunt validate \;
```

## After Running

- Report the total violation count for each tool separately.
- For failures, show file path, line number (where available), and rule name.
- If running with `--fix`, report how many issues were auto-fixed and how many remain.
- If TypeScript type errors exist, list them separately from lint errors.
- Summarize with a pass/fail status per category:
  - .NET Format: PASS/FAIL
  - ESLint: PASS/FAIL
  - TypeScript: PASS/FAIL
  - Helm Lint: PASS/FAIL
  - Terraform Validate: PASS/FAIL
  - Terraform Format: PASS/FAIL
