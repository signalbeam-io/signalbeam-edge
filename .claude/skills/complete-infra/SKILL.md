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

### Phase 1: Parallel Infrastructure Lint

Launch all three lint tracks in parallel using separate Bash tool calls in a single response.

**Track A: Terraform format check**
```bash
terraform fmt -check -recursive infra
```

**Track B: Terraform validate (changed modules only)**
```bash
CHANGED=$(git diff --name-only origin/main...HEAD -- 'infra/terraform/')
if [ -n "$CHANGED" ]; then
  for dir in $(echo "$CHANGED" | xargs -I{} dirname {} | sort -u); do
    echo "=== Validating $dir ==="
    terraform -chdir="$dir" init -backend=false -input=false 2>/dev/null
    terraform -chdir="$dir" validate
  done
else
  echo "No Terraform changes to validate"
fi
```

**Track C: Helm lint (if charts changed)**
```bash
HELM_CHANGED=$(git diff --name-only origin/main...HEAD -- 'deploy/charts/')
if [ -n "$HELM_CHANGED" ]; then
  for chart in $(echo "$HELM_CHANGED" | cut -d'/' -f1-3 | sort -u); do
    echo "=== Linting $chart ==="
    helm lint "$chart"
    helm template test "$chart" > /dev/null
  done
else
  echo "No Helm changes to lint"
fi
```

If any lint track fails, STOP and report.

### Phase 2: Infrastructure Review (Parallel Agents)

Launch the review agent with worktree isolation:

**Agent: `infra-reviewer`** (isolation: worktree) — Dedicated Terraform/Helm/CI review using the `infra-reviewer` agent definition. Reviews `git diff origin/main...HEAD -- infra/ deploy/ .github/workflows/` and returns PASS/FAIL with findings covering security, naming, resource limits, and CI best practices.

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
- Helm Lint: PASS / SKIP (no chart changes)
- Infra Review: PASS

PR is ready for human review.
```
