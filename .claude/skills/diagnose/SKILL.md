---
name: diagnose
description: Investigate and diagnose problems using structured evidence gathering and hypothesis testing
allowed-tools: Bash, Read, Glob, Grep, Task, WebSearch
user-invocable: true
---

# Diagnose

Systematically investigate a problem using evidence-based reasoning. This skill follows a structured investigation methodology to find root causes and propose solutions.

## Arguments

- `{problem}` — Description of the problem to investigate (required)
- `--quick` — Fast diagnosis, skip deep analysis
- `--logs` — Include log analysis in investigation

## Process

### Phase 1: Problem Statement

Clarify the problem:
- What is the expected behavior?
- What is the actual behavior?
- When did it start? (commit, deployment, time)
- Is it reproducible? How?
- What's the impact? (severity, affected users)

### Phase 2: Evidence Collection

Use an investigator approach to gather evidence systematically.

**Source 1: Error Messages & Logs**
```bash
# Recent git history
git log --oneline -20

# Search for related errors in codebase
grep -r "ERROR_CODE" src/

# If --logs flag, search log patterns
grep -rn "{error pattern}" logs/
```

**Source 2: Code Analysis**
```bash
# Find related code
grep -rn "{symptom keyword}" src/

# Check recent changes to affected area
git log -p --since="1 week ago" -- {affected paths}

# Find usages and dependencies
grep -rn "{function/class name}" src/
```

**Source 3: Configuration**
```bash
# Check appsettings
cat src/**/appsettings*.json | grep -i "{related config}"

# Check environment variables
grep -rn "GetEnvironmentVariable\|GetValue<" src/ | grep -i "{related}"
```

**Source 4: Tests**
```bash
# Find related tests
grep -rn "{feature}" tests/

# Check if tests are passing
dotnet test --filter "{test pattern}" --no-build
```

**Source 5: External Context**
- Search for similar issues in GitHub issues
- Search for related error messages online
- Check documentation for expected behavior

### Phase 3: Hypothesis Generation

Based on evidence, generate hypotheses ranked by likelihood:

```markdown
## Hypotheses

| # | Hypothesis | Likelihood | Evidence For | Evidence Against |
|---|------------|------------|--------------|------------------|
| H1 | {hypothesis} | High | {evidence} | {counter-evidence} |
| H2 | {hypothesis} | Medium | {evidence} | {counter-evidence} |
| H3 | {hypothesis} | Low | {evidence} | {counter-evidence} |
```

### Phase 4: Hypothesis Testing

For each hypothesis (starting with highest likelihood):

1. **Identify test** — What would confirm/refute this hypothesis?
2. **Execute test** — Run the test (code inspection, running test, reproduction)
3. **Record result** — Did it confirm or refute?
4. **Update likelihood** — Adjust rankings based on result

```markdown
### Testing H1: {hypothesis}

**Test:** {what we'll do to test}
**Result:** {CONFIRMED / REFUTED / INCONCLUSIVE}
**Evidence:** {what we found}
**New likelihood:** {Updated: High/Medium/Low}
```

### Phase 5: Root Cause Analysis

Once a hypothesis is confirmed:

**5 Whys Analysis:**
1. Why did {symptom} happen? → {immediate cause}
2. Why did {immediate cause} happen? → {deeper cause}
3. Why did {deeper cause} happen? → {even deeper}
4. Why? → {root cause candidate}
5. Why? → {root cause}

**Contributing Factors:**
- {Factor 1}
- {Factor 2}

### Phase 6: Solution Proposal

```markdown
## Proposed Solutions

### Solution 1: {name} (Recommended)

**Approach:** {description}

**Changes Required:**
- {file}: {change description}
- {file}: {change description}

**Pros:**
- {advantage}

**Cons:**
- {disadvantage}

**Risk:** {Low/Medium/High}
**Effort:** {Small/Medium/Large}

---

### Solution 2: {name} (Alternative)

{same structure}

---

### Recommendation

{Which solution and why}
```

### Phase 7: Prevention

```markdown
## Prevention Measures

### Immediate
- [ ] {Quick fix or guard}

### Short-term
- [ ] {Test to add}
- [ ] {Validation to add}

### Long-term
- [ ] {Architecture improvement}
- [ ] {Process change}
```

## Output Format

```markdown
# Diagnosis Report: {Problem Title}

**Reported:** {date}
**Severity:** {Critical/High/Medium/Low}
**Status:** {Investigating/Root Cause Found/Solution Proposed}

---

## Problem Statement

{Clear description of the problem}

**Expected:** {expected behavior}
**Actual:** {actual behavior}
**Reproduction:** {steps to reproduce}

---

## Evidence Summary

| Source | Finding | Relevance |
|--------|---------|-----------|
| {source} | {finding} | {High/Medium/Low} |

---

## Investigation Timeline

1. {timestamp} — {action taken} → {result}
2. {timestamp} — {action taken} → {result}

---

## Root Cause

**Confirmed Cause:** {root cause description}

**5 Whys:**
{analysis}

**Contributing Factors:**
- {factor}

---

## Solutions

{Solution proposals from Phase 6}

---

## Prevention

{Prevention measures from Phase 7}

---

## Appendix: Raw Evidence

<details>
<summary>Evidence Details</summary>

{Detailed evidence collected}

</details>
```

## Quick Mode (--quick)

For urgent issues, use abbreviated process:
1. Collect obvious evidence (2 min)
2. Form top hypothesis
3. Test immediately
4. Propose quick fix
5. Note: "Full diagnosis recommended after fix"

## Guidelines

- Follow the evidence — don't jump to conclusions
- Document everything — future you will thank past you
- Consider multiple hypotheses — avoid confirmation bias
- Test hypotheses — don't assume, verify
- Think systematically — use the 5 Whys
- Propose prevention — fix the system, not just the symptom
