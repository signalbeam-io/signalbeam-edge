---
name: complete-infra
description: Complete infrastructure task — lint, validate, review, and submit PR. Use when an infrastructure-only branch is ready for submission — runs Terraform/Helm validation and creates the PR, skipping .NET/frontend checks.
allowed-tools: Bash, Read, Glob, Grep, Task, Skill
user-invocable: true
---

# Complete Infrastructure Task

Finalize an infrastructure branch by running infra-specific quality gates and creating a PR. This is the infra equivalent of `/complete-task` — it skips .NET build/tests and frontend lint.

## Prerequisites

- You must be on a feature branch (not main)
- All infrastructure changes should be committed
- Working tree should be clean

## Arguments

- `{issue}` — GitHub issue number (optional). If not provided, extract from branch name.

## Process

### Phase 0: Pre-flight

```bash
BRANCH=$(git branch --show-current)
if [ "$BRANCH" = "main" ] || [ "$BRANCH" = "master" ]; then
  echo "ERROR: Cannot complete task on main branch"
  exit 1
fi

if [ -n "$(git status --porcelain)" ]; then
  echo "ERROR: Working tree is dirty. Commit or stash changes first."
  exit 1
fi

ISSUE=$(echo "$BRANCH" | grep -oE '/[0-9]+' | tr -d '/')
echo "Issue: #$ISSUE"
```

### Phase 1: Infrastructure Lint

Run all infra linting:

**Terraform format check:**
```bash
terraform fmt -check -recursive infra
```

**Terraform validate (changed modules only):**
```bash
# Get list of changed terraform files
CHANGED=$(git diff --name-only origin/main...HEAD -- 'infra/terraform/')
if [ -n "$CHANGED" ]; then
  for dir in $(echo "$CHANGED" | xargs -I{} dirname {} | sort -u); do
    echo "=== Validating $dir ==="
    terraform -chdir="$dir" init -backend=false -input=false 2>/dev/null
    terraform -chdir="$dir" validate
  done
fi
```

**Helm lint (if charts changed):**
```bash
HELM_CHANGED=$(git diff --name-only origin/main...HEAD -- 'deploy/charts/')
if [ -n "$HELM_CHANGED" ]; then
  for chart in $(echo "$HELM_CHANGED" | cut -d'/' -f1-3 | sort -u); do
    echo "=== Linting $chart ==="
    helm lint "$chart"
    helm template test "$chart" > /dev/null
  done
fi
```

If any lint step fails, STOP and report.

### Phase 2: Infrastructure Review

Launch a subagent to review infrastructure changes:

**Subagent: `reviewer`** — Review `git diff origin/main...HEAD` focusing on:
- Security: exposed secrets, overly permissive IAM/RBAC, public endpoints
- Best practices: naming conventions, tagging, resource sizing
- Dependencies: correct Terragrunt dependency ordering
- Helm: template correctness, value defaults, resource limits

The subagent should return PASS/FAIL with findings.

### Phase 3: Evaluate

**If review passes:** Proceed to Phase 4.

**If issues found:**
1. Display findings
2. Fix issues (max 3 iterations)
3. Re-run from Phase 1

### Phase 4: Create PR

Run `/create-pr {issue}`.

## Output

```
## Infrastructure Task Completed

- Branch: {branch}
- Issue: #{issue}
- PR: {pr-url}

### Quality Gates
- Terraform Format: PASS
- Terraform Validate: PASS
- Terragrunt Validate: PASS
- Helm Lint: PASS/SKIPPED
- Infra Review: PASS

PR is ready for human review.
```
