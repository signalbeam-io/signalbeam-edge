---
name: complete-task
description: Complete current task with all quality gates, code review, QA check, and submit PR
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

**Step 1.1: Build**
```bash
dotnet build src/SignalBeam.sln --configuration Release --no-restore
```

**Step 1.2: Lint (with optional auto-fix)**
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

**Step 1.3: Helm & Terraform**
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

Launch TWO subagents in parallel using the Task tool:

**Subagent 1: Code Review**
```
You are a code reviewer for SignalBeam Edge.

Review all changes on this branch compared to origin/main.

Run: git diff origin/main...HEAD

Check for:
1. Security vulnerabilities (OWASP top 10: injection, XSS, auth bypass)
2. Result pattern violations (throwing exceptions for business logic)
3. Hexagonal architecture violations (Domain depending on Infrastructure)
4. Missing error handling or swallowed exceptions
5. Code duplication that should be extracted
6. Missing or inadequate tests for new functionality
7. Hardcoded secrets or configuration values
8. Breaking API changes without versioning

Output format:
## Code Review Results

### Critical Issues (must fix)
- [ ] {file}:{line} — {description}

### Warnings (should fix)
- [ ] {file}:{line} — {description}

### Suggestions (nice to have)
- {description}

### Summary
{PASS | FAIL with issue count}
```

**Subagent 2: Task Check (QA)**
```
You are a QA reviewer for SignalBeam Edge.

Fetch the GitHub issue for this branch and verify implementation matches requirements.

1. Get issue number from branch: git branch --show-current
2. Fetch issue: gh issue view {number} --json title,body,labels
3. Review the acceptance criteria in the issue body
4. Check what was implemented: git diff origin/main...HEAD --stat

For each acceptance criterion:
- Mark as MET if the implementation satisfies it
- Mark as UNMET if missing or incomplete
- Mark as PARTIAL if partially implemented

Output format:
## Task Check Results

### Issue: #{number} — {title}

### Acceptance Criteria
- [x] AC1: {criterion} — MET: {evidence}
- [ ] AC2: {criterion} — UNMET: {what's missing}
- [~] AC3: {criterion} — PARTIAL: {what's done, what's missing}

### Summary
{PASS | FAIL — X of Y criteria met}
```

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
