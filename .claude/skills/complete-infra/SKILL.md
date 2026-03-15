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

## Scripts

Reusable bash scripts live in `scripts/` (skill-local) and `.claude/scripts/` (shared):

| Script | Location | Purpose |
|--------|----------|---------|
| `preflight.sh` | `.claude/scripts/` | Branch check, clean tree, issue extraction |
| `validate-terraform.sh` | `scripts/` | Validate changed Terraform modules |
| `lint-helm.sh` | `scripts/` | Lint changed Helm charts |

## Process

### Phase 0: Pre-flight

Run the shared pre-flight script:

```bash
source .claude/scripts/preflight.sh
```

### Phase 1: Parallel Infrastructure Lint

Launch all three lint tracks in parallel using separate Bash tool calls in a single response.

**Track A: Terraform format check**
```bash
terraform fmt -check -recursive infra
```

**Track B: Terraform validate (changed modules only)**
```bash
bash .claude/skills/complete-infra/scripts/validate-terraform.sh
```

**Track C: Helm lint (if charts changed)**
```bash
bash .claude/skills/complete-infra/scripts/lint-helm.sh
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
