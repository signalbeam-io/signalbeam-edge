---
name: complete-task
description: Complete current task with all quality gates, code review, QA check, and submit PR. Use when implementation is done and you want to run the full verification pipeline (build, lint, tests, review, QA) and create the pull request.
allowed-tools: Bash, Read, Glob, Grep, Task, Skill
user-invocable: true
---

# Complete Task

Finalize the current feature branch by running all quality gates and creating a PR. This skill orchestrates multiple verification steps with parallel execution where possible, and dynamically composes review agents based on what changed.

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
[start] → pre-flight + detect-changes
    ↓
  parallel-build (backend || frontend)
    ↓
  pending-migrations (if HAS_BACKEND)
    ↓
  parallel-lint+tests (backend || frontend || infra)
    ↓
  integration-tests → browser-verify
    ↓
  parallel-agents (reviewer || verifier || doc-checker? || infra-reviewer?)
    ↓
  [issues?] → scoped-fix → [restart affected tracks]
    ↓
  create-pr → [done]
```

CRITICAL: Do not skip states. Do not proceed if a gate fails. Maximum 3 fix iterations before stopping.

## Process

### Phase 0: Pre-flight + Change Detection

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

**Change Detection — run immediately after pre-flight:**

```bash
CHANGED_FILES=$(git diff --name-only origin/main...HEAD)

HAS_BACKEND=$(echo "$CHANGED_FILES" | grep -c "^src/" || true)
HAS_FRONTEND=$(echo "$CHANGED_FILES" | grep -c "^web/" || true)
HAS_INFRA=$(echo "$CHANGED_FILES" | grep -cE "^(infra/|deploy/|\.github/workflows/)" || true)
HAS_ENDPOINTS=$(echo "$CHANGED_FILES" | grep -c "Endpoints/" || true)
HAS_ENTITIES=$(echo "$CHANGED_FILES" | grep -c "Domain/Entities/" || true)
HAS_EVENTS=$(echo "$CHANGED_FILES" | grep -c "Domain/Events/" || true)

echo "Change detection:"
echo "  Backend:   $([ $HAS_BACKEND -gt 0 ] && echo YES || echo NO)"
echo "  Frontend:  $([ $HAS_FRONTEND -gt 0 ] && echo YES || echo NO)"
echo "  Infra:     $([ $HAS_INFRA -gt 0 ] && echo YES || echo NO)"
echo "  Endpoints: $([ $HAS_ENDPOINTS -gt 0 ] && echo YES || echo NO)"
echo "  Entities:  $([ $HAS_ENTITIES -gt 0 ] && echo YES || echo NO)"
echo "  Events:    $([ $HAS_EVENTS -gt 0 ] && echo YES || echo NO)"
```

These flags gate all downstream phases. If a flag is 0, skip its corresponding tracks.

### Phase 1: Parallel Build

Launch build commands in parallel using separate Bash tool calls in a single response. Only include tracks where changes were detected.

**Track A (if HAS_BACKEND > 0):**
```bash
dotnet build src/SignalBeam.sln --configuration Release --no-restore
```

**Track B (if HAS_FRONTEND > 0):**
```bash
cd web && npm run type-check
```

If either track fails, STOP and report the failure.

### Phase 1.5: Pending Migrations Check (if HAS_BACKEND > 0)

```bash
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

### Phase 2: Parallel Lint + Unit Tests

Launch all applicable tracks in parallel using separate Bash tool calls in a single response.

**Track A (if HAS_BACKEND > 0): Backend lint + unit tests**

If `--auto-fix` was passed, run `dotnet format src/SignalBeam.sln` first, then:
```bash
dotnet format src/SignalBeam.sln --verify-no-changes && dotnet test src/SignalBeam.sln --no-build --configuration Release --filter "Category!=Integration"
```

**Track B (if HAS_FRONTEND > 0): Frontend lint**

If `--auto-fix` was passed, run `cd web && npm run lint:fix` first, then:
```bash
cd web && npm run lint
```

**Track C (if HAS_INFRA > 0): Infrastructure lint**
```bash
helm lint deploy/charts/signalbeam-infrastructure && helm lint deploy/charts/signalbeam-platform && terraform fmt -check -recursive infra
```

If any track fails, STOP and report which track(s) failed.

### Phase 3: Integration Tests (if HAS_BACKEND > 0)

Skip if `--skip-integration` was passed.

```bash
dotnet test src/SignalBeam.sln --no-build --configuration Release --filter "Category=Integration"
```

If tests fail, STOP and report failures.

### Phase 3.5: Browser Verification (if HAS_FRONTEND > 0)

Check if the frontend is running:

```bash
curl -sf http://localhost:5173 > /dev/null 2>&1 && echo "Frontend: UP" || echo "Frontend: DOWN"
```

- **Frontend running:** Invoke `/smoke-test --frontend-only` to verify routes render without errors. Report results as **advisory (WARNING, not FAIL)** — browser verification issues don't block the PR.
- **Frontend not running:** Skip with advisory note: "Browser verification skipped — frontend not running. Run `/run-local` and `/verify-feature` to test manually."

### Phase 4: Parallel Agent Review (Dynamic Composition)

Launch all applicable agents in parallel in a single response. Each agent uses `isolation: "worktree"` for safe parallel reads.

**Always launch:**

1. **`reviewer`** (isolation: worktree) — Code review for security, architecture, and quality issues. Uses the `reviewer` agent definition. The agent should review `git diff origin/main...HEAD` and return a structured report with Critical/Warning/Suggestion categories and a PASS/FAIL summary.

2. **`verifier`** (isolation: worktree) — QA verification that implementation matches the GitHub issue acceptance criteria. Uses the `verifier` agent definition. The agent should fetch the issue via `gh issue view`, compare against the diff, and return MET/UNMET/PARTIAL status for each criterion with a PASS/FAIL summary.

**Conditionally launch:**

3. **`doc-checker`** (isolation: worktree) — Only if `HAS_ENDPOINTS > 0` OR `HAS_ENTITIES > 0` OR `HAS_EVENTS > 0` OR `HAS_INFRA > 0`. Detects stale documentation relative to code changes.

4. **`infra-reviewer`** (isolation: worktree) — Only if `HAS_INFRA > 0`. Dedicated Terraform/Helm/CI review using the `infra-reviewer` agent definition.

### Phase 5: Evaluate + Scoped Fix Loop

Collect results from all agents.

**If ALL pass:**
- Proceed to Phase 6

**If ANY issues found:**
1. Display the issues to the user, grouped by agent
2. Ask: "Fix these issues automatically? (max 3 iterations)"
3. If yes, fix the issues
4. Re-run only affected tracks — determine which tracks to re-run based on which files the fix touched:
   - Fix touched `src/` → re-run backend build, backend lint+tests
   - Fix touched `web/` → re-run frontend build, frontend lint
   - Fix touched `infra/` or `deploy/` → re-run infra lint
   - Re-run only the agents that reported issues (not all agents)
   - When unsure which tracks a fix affects, re-run all tracks
5. If no or max iterations reached, STOP and report

Track iteration count. After 3 failed attempts, STOP with:
```
Maximum fix iterations reached. Manual intervention required.
Remaining issues:
{list issues}
```

### Phase 5.5: Documentation Check (Advisory)

Before creating the PR, if `HAS_ENDPOINTS > 0` OR `HAS_ENTITIES > 0` OR `HAS_EVENTS > 0` OR `HAS_INFRA > 0`, suggest running `/docs` for the affected areas:
- New/changed endpoints → `/docs api {service}`
- New/changed entities or events → `/docs domain`
- Infrastructure changes → `/docs architecture`

This is advisory, not blocking — note it in the PR output if docs may need updating. If the `doc-checker` agent already ran in Phase 4, use its findings here instead of re-checking.

### Phase 6: Create PR

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
- Build: PASS (backend + frontend) / PASS (backend only) / PASS (frontend only)
- Lint: PASS (backend + frontend + infra) / PASS ({active tracks})
- Unit Tests: PASS ({count} tests) / SKIP (no backend changes)
- Integration Tests: PASS ({count} tests) / SKIP
- Browser Verification: PASS / SKIP (no web/ changes) / SKIP (frontend not running)
- Code Review: PASS
- Task Check: PASS ({x}/{y} criteria met)
- Doc Check: PASS (no stale docs) / SKIP (no endpoint/entity/event/infra changes)
- Infra Review: PASS / SKIP (no infra changes)

PR is ready for human review.
```

On failure:
```
## Task Completion Failed

Failed at: {phase name}
Reason: {error details}

{Specific failure output}
```

## When NOT to Use

- **Infrastructure-only changes** (only `infra/`, `deploy/`, `.github/workflows/`) — use `/complete-infra` instead, which skips .NET build/tests and frontend lint
- **Work in progress** — this skill expects all implementation to be complete and committed
- **Exploratory changes** — if you're still figuring out the approach, run `/check-architecture` and `/run-tests` individually instead

## Guidelines

- This skill is idempotent — safe to run multiple times
- All bash commands use explicit paths relative to repo root
- Subagents run with worktree isolation to avoid polluting main conversation and enable safe parallel reads
- Never force-push or amend commits during this process
- If unsure about a fix, ask the user rather than guessing
- Parallel tracks are launched as multiple tool calls in a single response for maximum throughput
