---
name: task-check
description: QA check to verify implementation matches GitHub issue requirements. Use to validate that all acceptance criteria from the linked issue are met before creating a PR.
allowed-tools: Bash, Read, Glob, Grep, mcp__github-mcp-server__get_issue
user-invocable: true
---

# Task Check (QA Verification)

Verify that the implementation on the current branch satisfies the requirements defined in the linked GitHub issue. This is a QA gate that ensures we built what was asked for.

## Arguments

- `{issue}` — GitHub issue number (optional, extracted from branch name if not provided)
- `--strict` — Fail on any unmet criterion (default: fail only on "Must Have" criteria)

## Process

### Step 1: Identify the Issue

```bash
# Get current branch
BRANCH=$(git branch --show-current)

# Extract issue number from branch name (e.g., makigjuro/42-feature-name)
ISSUE=$(echo "$BRANCH" | grep -oE '/[0-9]+' | tr -d '/')

# If no issue in branch name and not provided as argument, fail
if [ -z "$ISSUE" ]; then
  echo "ERROR: Could not determine issue number. Provide as argument: /task-check 42"
  exit 1
fi
```

### Step 2: Fetch Issue Details

Use MCP for structured issue data:

```
mcp__github-mcp-server__get_issue(owner: "signalbeam-io", repo: "signalbeam-edge", issue_number: $ISSUE)
```

This returns structured JSON with number, title, body, labels, state, and comments — no CLI output parsing needed.

### Step 3: Parse Acceptance Criteria

Extract acceptance criteria from the issue body. Look for:
- Checkbox lists: `- [ ] Criterion`
- "Acceptance Criteria" section
- "Requirements" section
- "Definition of Done" section
- User stories with "so that" format

Categorize each criterion:
- **Must Have:** Explicitly marked as required, or in "Acceptance Criteria"
- **Should Have:** Listed but not critical
- **Nice to Have:** Suggestions, future improvements mentioned

### Step 4: Analyze Implementation

For each acceptance criterion, search the codebase to verify it was implemented:

```bash
# Get all changes on this branch
git diff origin/main...HEAD --name-only

# Get the actual changes
git diff origin/main...HEAD
```

**Verification methods by criterion type:**

| Criterion Type | How to Verify |
|----------------|---------------|
| New endpoint | Search for route in Host/Endpoints |
| New entity | Search in Domain/Entities |
| New command/query | Search in Application/Commands or Queries |
| UI feature | Search in web/src/components or pages |
| Validation rule | Search for FluentValidation rule |
| Error handling | Search for Result.Failure with specific error code |
| Test coverage | Search in tests/ for relevant test methods |
| Documentation | Check for updated docs or comments |

### Step 5: Evidence Gathering

For each criterion, collect evidence:

- **File paths** where the implementation exists
- **Code snippets** showing the implementation
- **Test names** that cover the requirement
- **Commit messages** that reference the criterion

### Step 6: Generate Report

```markdown
## Task Check: Issue #{number}

**Title:** {issue title}
**Branch:** {branch name}
**Labels:** {labels}

---

### Acceptance Criteria Verification

#### Must Have

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | {criterion text} | {MET/UNMET/PARTIAL} | {file:line or description} |
| 2 | {criterion text} | {MET/UNMET/PARTIAL} | {file:line or description} |

**Details:**

##### Criterion 1: {criterion}
- **Status:** MET
- **Evidence:**
  - Implementation: `src/DeviceManager/Application/Commands/RegisterDevice.cs:45`
  - Test: `tests/DeviceManager.Tests/RegisterDeviceHandlerTests.cs:TestRegistration`
  ```csharp
  {relevant code snippet}
  ```

##### Criterion 2: {criterion}
- **Status:** UNMET
- **Missing:** {what's missing}
- **Suggestion:** {how to implement}

---

#### Should Have

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | {criterion} | {status} | {evidence} |

---

#### Nice to Have (Not Required)

| # | Criterion | Status | Notes |
|---|-----------|--------|-------|
| 1 | {criterion} | {DONE/SKIPPED} | {note} |

---

### Implementation Summary

**Files Changed:** {count}
**New Files:** {list}
**Tests Added:** {count}

**Key Changes:**
- {summary of major change 1}
- {summary of major change 2}

---

### Gaps Identified

{If any criteria are UNMET or PARTIAL:}

1. **{Criterion}**
   - Missing: {description}
   - Effort estimate: {Small/Medium/Large}
   - Suggested implementation: {brief approach}

---

### Summary

| Category | Met | Unmet | Partial | Total |
|----------|-----|-------|---------|-------|
| Must Have | {n} | {n} | {n} | {n} |
| Should Have | {n} | {n} | {n} | {n} |
| Nice to Have | {n} | {n} | {n} | {n} |

**Result: {PASS / FAIL}**

{If PASS: "All Must Have criteria are met. Ready for code review."}
{If FAIL: "X Must Have criteria are unmet. See Gaps Identified above."}

---

### Recommendations

{If PASS with warnings:}
- Consider addressing Should Have items: {list}
- Nice to Have items deferred to future: {list}

{If FAIL:}
- Priority fixes needed:
  1. {Most critical gap}
  2. {Second priority}
```

## Handling Edge Cases

**No acceptance criteria in issue:**
- Look for implicit requirements in the description
- Check comments for clarifications
- If truly undefined, WARN and suggest adding criteria before proceeding

**Vague criteria:**
- Flag as "NEEDS CLARIFICATION"
- Make best-effort assessment
- Include recommendation to clarify with stakeholder

**Scope creep detected:**
- If implementation includes features not in issue, note as "EXTRA"
- Not a failure, but worth flagging for review

## Integration with /complete-task

When called as an agent from `/complete-task`:
- Return pass/fail status and count of unmet Must Have criteria
- Include list of gaps if any
- Unmet Must Have criteria cause the parent workflow to pause

## Guidelines

- Be thorough but practical — focus on what matters
- Evidence is key — always show where something is implemented
- Partial credit counts — note progress even if incomplete
- Consider intent — sometimes requirements evolve during implementation
- Flag scope creep — extra work is good but should be acknowledged
