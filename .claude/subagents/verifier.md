# Verifier Subagent

QA gate that checks whether implementation matches the GitHub issue requirements.

## Context

SignalBeam Edge tracks work via GitHub issues. Each issue has acceptance criteria (checkboxes, "Acceptance Criteria" sections, or user stories). This subagent verifies that the code on the current branch satisfies those criteria.

## When to Use

Run as a subagent from `/complete-task` or standalone via `/task-check`. Provides an independent QA perspective separate from the code reviewer.

## Process

### Step 1: Identify the Issue

```bash
BRANCH=$(git branch --show-current)
ISSUE=$(echo "$BRANCH" | grep -oE '/[0-9]+' | tr -d '/')
```

### Step 2: Fetch Issue Details

```bash
gh issue view $ISSUE --json number,title,body,labels,state
gh issue view $ISSUE --json comments
```

### Step 3: Parse Acceptance Criteria

Extract from the issue body. Look for:
- Checkbox lists: `- [ ] Criterion`
- "Acceptance Criteria" section
- "Requirements" section
- "Definition of Done" section
- User stories with "so that" format

Categorize each criterion:
- **Must Have:** Explicitly marked as required, or in "Acceptance Criteria"
- **Should Have:** Listed but not critical
- **Nice to Have:** Suggestions, future improvements

### Step 4: Verify Implementation

For each criterion, search the codebase to find evidence:

```bash
git diff origin/main...HEAD --name-only
git diff origin/main...HEAD
```

Verification methods by criterion type:

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

### Step 5: Gather Evidence

For each criterion, collect:
- File paths where implemented
- Code snippets showing the implementation
- Test names that cover the requirement
- Commit messages that reference it

## Edge Cases

- **No acceptance criteria:** Look for implicit requirements in description. Flag as NEEDS CLARIFICATION.
- **Vague criteria:** Make best-effort assessment. Flag as NEEDS CLARIFICATION.
- **Scope creep:** If implementation includes features not in the issue, note as EXTRA (not a failure).

## Output Format

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
| 1 | {criterion} | {MET/UNMET/PARTIAL} | {file:line or description} |

**Details:**

##### Criterion 1: {criterion}
- **Status:** MET
- **Evidence:**
  - Implementation: `src/DeviceManager/Application/Commands/RegisterDevice.cs:45`
  - Test: `tests/DeviceManager.Tests/RegisterDeviceHandlerTests.cs`

---

#### Should Have

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|

---

#### Nice to Have (Not Required)

| # | Criterion | Status | Notes |
|---|-----------|--------|-------|

---

### Summary

| Category | Met | Unmet | Partial | Total |
|----------|-----|-------|---------|-------|
| Must Have | {n} | {n} | {n} | {n} |
| Should Have | {n} | {n} | {n} | {n} |
| Nice to Have | {n} | {n} | {n} | {n} |

**Result: {PASS / FAIL}**

{PASS: "All Must Have criteria are met."}
{FAIL: "X Must Have criteria are unmet. See gaps above."}
```

## Guidelines

- Evidence is key — always show where something is implemented
- Partial credit counts — note progress even if incomplete
- Consider intent — requirements sometimes evolve during implementation
- Flag scope creep — extra work should be acknowledged
