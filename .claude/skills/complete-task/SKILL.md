---
name: complete-task
description: Complete current task with all quality gates, code review, QA check, and submit PR. Use when implementation is done and you want to run the full verification pipeline (build, lint, tests, review, QA) and create the pull request.
allowed-tools: Bash, Read, Glob, Grep, Task, Skill
user-invocable: true
---

# Complete Task

Finalize the current feature branch by running all quality gates and creating a PR. This skill orchestrates multiple verification steps and uses subagents for code review and task validation.

## Prerequisites

- You must be on a feature branch (not main)
- All implementation work should be complete
- Working tree should be clean (all changes committed)

## Arguments

- `{issue}` — GitHub issue number (optional). If not provided, extract from branch name.
- `--skip-integration` — Skip integration tests (faster, for WIP checks)
- `--auto-fix` — Automatically fix lint issues before proceeding

## State Machine

```
[start] → verify-branch → build → lint → unit-tests → integration-tests
    ↓
  review+qa (parallel subagents)
    ↓
  [issues?] → fix → [restart from build]
    ↓
  create-pr → [done]
```

CRITICAL: Do not skip states. Do not proceed if a gate fails. Maximum 3 fix iterations before stopping.

## Process

### Phase 0: Pre-flight Checks

```bash
# Verify we're on a feature branch
BRANCH=$(git branch --show-current)
if [ "$BRANCH" = "main" ] || [ "$BRANCH" = "master" ]; then
  echo "ERROR: Cannot complete task on main branch"
  exit 1
fi

# Check for uncommitted changes
if [ -n "$(git status --porcelain)" ]; then
  echo "ERROR: Working tree is dirty. Commit or stash changes first."
  exit 1
fi

# Extract issue number from branch name (e.g., makigjuro/42-feature-name)
ISSUE=$(echo "$BRANCH" | grep -oE '/[0-9]+' | tr -d '/')
echo "Issue: #$ISSUE"
```

If pre-flight fails, STOP and report the issue.

### Phase 1: Build & Lint (Sequential, Deterministic)

Run these bash commands in order. If any fail, STOP.

**Step 1.1: Pending Migrations Check**

Check for pending model changes that need a migration. This catches missing migrations before they cause runtime errors.

```bash
# For each service with a DbContext, check for pending changes
for service in DeviceManager BundleOrchestrator TelemetryProcessor IdentityManager; do
  infra=$(find src -path "*/$service*Infrastructure*.csproj" | head -1)
  host=$(find src -path "*/$service*Host*.csproj" | head -1)
  if [ -n "$infra" ] && [ -n "$host" ]; then
    echo "Checking $service for pending migrations..."
    dotnet ef migrations has-pending-model-changes --project "$infra" --startup-project "$host" 2>&1 || true
  fi
done
```

If any service reports pending changes, STOP and create the migration using `/add-migration` before proceeding.

**Step 1.2: Build**
```bash
dotnet build src/SignalBeam.sln --configuration Release --no-restore
```

**Step 1.3: Lint (with optional auto-fix)**
If `--auto-fix` was passed:
```bash
dotnet format src/SignalBeam.sln
cd web && npm run lint:fix && cd ..
```

Then verify:
```bash
dotnet format src/SignalBeam.sln --verify-no-changes
cd web && npm run lint && npm run type-check && cd ..
```

**Step 1.4: Helm & Terraform**
```bash
helm lint deploy/charts/signalbeam-infrastructure
helm lint deploy/charts/signalbeam-platform
terraform fmt -check -recursive infra
```

### Phase 2: Tests (Sequential)

**Step 2.1: Unit Tests**
```bash
dotnet test src/SignalBeam.sln --no-build --configuration Release --filter "Category!=Integration"
```

**Step 2.2: Integration Tests** (skip if `--skip-integration`)
```bash
dotnet test src/SignalBeam.sln --no-build --configuration Release --filter "Category=Integration"
```

If tests fail, STOP and report failures.

### Phase 3: Quality Review (Parallel Subagents)

Launch TWO subagents in parallel using the Task tool. Each reads its instructions from `.claude/subagents/` on startup.

**Subagent 1: `reviewer`** — Code review for security, architecture, and quality issues. Uses the `reviewer` subagent definition. The subagent should review `git diff origin/main...HEAD` and return a structured report with Critical/Warning/Suggestion categories and a PASS/FAIL summary.

**Subagent 2: `verifier`** — QA verification that implementation matches the GitHub issue acceptance criteria. Uses the `verifier` subagent definition. The subagent should fetch the issue via `gh issue view`, compare against the diff, and return MET/UNMET/PARTIAL status for each criterion with a PASS/FAIL summary.

### Phase 4: Evaluate Results

Collect results from both subagents.

**If BOTH pass:**
- Proceed to Phase 5

**If ANY issues found:**
1. Display the issues to the user
2. Ask: "Fix these issues automatically? (max 3 iterations)"
3. If yes, fix the issues and restart from Phase 1
4. If no or max iterations reached, STOP and report

Track iteration count. After 3 failed attempts, STOP with:
```
Maximum fix iterations reached. Manual intervention required.
Remaining issues:
{list issues}
```

### Phase 4.5: Documentation Check (Optional)

Before creating the PR, check if documentation should be updated:

```bash
# Check if endpoints or entities were added/modified
CHANGED_FILES=$(git diff --name-only origin/main...HEAD)
HAS_ENDPOINTS=$(echo "$CHANGED_FILES" | grep -c "Endpoints/" || true)
HAS_ENTITIES=$(echo "$CHANGED_FILES" | grep -c "Domain/Entities/" || true)
HAS_EVENTS=$(echo "$CHANGED_FILES" | grep -c "Domain/Events/" || true)
HAS_INFRA=$(echo "$CHANGED_FILES" | grep -cE "(infra/|deploy/)" || true)
```

If endpoints, entities, events, or infrastructure changed, suggest running `/docs` for the affected areas:
- New/changed endpoints → `/docs api {service}`
- New/changed entities or events → `/docs domain`
- Infrastructure changes → `/docs architecture`

This is advisory, not blocking — note it in the PR output if docs may need updating.

### Phase 5: Create PR

Run the `/create-pr` skill with the extracted issue number.

```
/create-pr {issue}
```

## Output

On success:
```
## Task Completed Successfully

- Branch: {branch}
- Issue: #{issue}
- PR: {pr-url}

### Quality Gates
- Build: PASS
- Lint: PASS
- Unit Tests: PASS ({count} tests)
- Integration Tests: PASS ({count} tests)
- Code Review: PASS
- Task Check: PASS ({x}/{y} criteria met)

PR is ready for human review.
```

On failure:
```
## Task Completion Failed

Failed at: {phase name}
Reason: {error details}

{Specific failure output}
```

## Guidelines

- This skill is idempotent — safe to run multiple times
- All bash commands use explicit paths relative to repo root
- Subagents run with isolated context to avoid polluting main conversation
- Never force-push or amend commits during this process
- If unsure about a fix, ask the user rather than guessing
