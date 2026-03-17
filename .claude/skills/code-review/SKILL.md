---
name: code-review
description: Review code changes for security, architecture, and quality issues. Use for deep PR-level review — checks OWASP vulnerabilities, hexagonal architecture violations, code smells, and test gaps. For a quick architecture-only check, use /check-architecture instead.
allowed-tools: Bash, Read, Glob, Grep, mcp__signalbeam-validator__validate_all_layers, mcp__signalbeam-validator__check_result_pattern
user-invocable: true
---

# Code Review

Perform a comprehensive code review of changes on the current branch compared to main. This skill can be invoked standalone or used as part of `/complete-task`.

## Arguments

- `--security-only` — Focus only on security issues
- `--architecture-only` — Focus only on architecture violations
- `--diff {base}` — Compare against a specific base (default: origin/main)
- `{files}` — Review only specific files (space-separated)

## Process

### Step 1: Gather Changes

```bash
# Get list of changed files
git diff origin/main...HEAD --name-only

# Get full diff for analysis
git diff origin/main...HEAD

# Get commit messages for context
git log origin/main..HEAD --oneline
```

### Step 2: Security Review

Check for OWASP Top 10 vulnerabilities:

**Injection (A03:2021)**
- SQL: Look for string concatenation in queries, raw SQL without parameters
- Command: Look for `Process.Start`, `Bash` with user input
- Search patterns: `FromSqlRaw`, `ExecuteSqlRaw`, string interpolation in queries

**Broken Authentication (A07:2021)**
- Hardcoded credentials, API keys, connection strings
- Missing authorization attributes on endpoints
- Weak password validation

**Sensitive Data Exposure (A02:2021)**
- Logging sensitive data (passwords, tokens, PII)
- Returning sensitive fields in API responses
- Missing encryption for sensitive storage

**Security Misconfiguration (A05:2021)**
- Debug mode enabled in non-dev code
- Overly permissive CORS
- Missing security headers
- Default credentials

**XSS (A03:2021)**
- React: `dangerouslySetInnerHTML` without sanitization
- Unescaped user input in responses

### Step 3: Architecture Review

Use MCP tools for automated checks, then supplement with manual review.

**Hexagonal Architecture — call `mcp__signalbeam-validator__validate_all_layers`:**

Returns structured JSON with pass/fail per service. Report any violations found.

**Result Pattern — call `mcp__signalbeam-validator__check_result_pattern` for each changed handler file:**

Identify handler files from the diff (`*Handler.cs` in Application layers) and check each one. The tool flags thrown business exceptions that should return `Result<T>` instead.

**CQRS Violations (manual check):**
- Commands in Query folders or vice versa
- Query handlers calling write repositories
- Command handlers returning data (queries in disguise)

**Domain Rules (manual check):**
- Entities with public setters (should be private/protected)
- Missing factory methods (public constructors on aggregates)
- Domain logic in Infrastructure layer
- Anemic entities (all logic in handlers)

### Step 4: Quality Review

**Code Smells**
- Methods longer than 30 lines
- Classes longer than 300 lines
- Deep nesting (> 3 levels)
- Magic numbers/strings without constants
- Duplicate code blocks (> 5 lines repeated)

**Error Handling**
- Empty catch blocks
- Catching generic `Exception` without re-throwing
- Missing null checks on external data
- Missing validation on API inputs

**Testing Gaps**
- New public methods without corresponding tests
- New handlers without test coverage
- Modified business logic without updated tests

**Naming & Conventions**
- Non-descriptive names (data, info, temp, x)
- Inconsistent naming with existing code
- Missing XML docs on public APIs

### Step 5: Generate Report

Output in this format:

```markdown
## Code Review: {branch-name}

**Reviewed:** {file count} files, {addition count} additions, {deletion count} deletions
**Commits:** {commit count}

---

### Critical Issues (Block PR)

These MUST be fixed before merging:

| # | File | Line | Issue | Rule |
|---|------|------|-------|------|
| 1 | {file} | {line} | {description} | {rule violated} |

**Details:**

#### Issue 1: {title}
- **File:** `{path}:{line}`
- **Code:**
  ```csharp
  {offending code snippet}
  ```
- **Problem:** {why this is an issue}
- **Fix:** {how to fix it}

---

### Warnings (Should Fix)

These should be addressed but don't block:

| # | File | Line | Issue | Rule |
|---|------|------|-------|------|
| 1 | {file} | {line} | {description} | {rule} |

---

### Suggestions (Nice to Have)

Improvements that would enhance code quality:

- {suggestion 1}
- {suggestion 2}

---

### Positive Observations

Things done well in this change:

- {positive 1}
- {positive 2}

---

### Summary

| Category | Status | Issues |
|----------|--------|--------|
| Security | {PASS/FAIL} | {count} |
| Architecture | {PASS/FAIL} | {count} |
| Quality | {PASS/WARN/FAIL} | {count} |
| Tests | {PASS/WARN} | {count} |

**Overall: {APPROVED / CHANGES REQUESTED / BLOCKED}**

{If APPROVED: "Ready to merge after addressing warnings (optional)"}
{If CHANGES REQUESTED: "Please address the issues above and re-request review"}
{If BLOCKED: "Critical issues must be resolved before this can proceed"}
```

## Severity Levels

- **Critical (Block):** Security vulnerabilities, data loss risks, architecture violations
- **Warning (Should Fix):** Code smells, missing tests, convention violations
- **Suggestion (Nice to Have):** Style improvements, refactoring opportunities

## Relationship to /check-architecture

`/code-review` and `/check-architecture` have complementary scopes:

| Aspect | `/check-architecture` | `/code-review` |
|--------|----------------------|----------------|
| Speed | Fast (local checks) | Thorough (full diff analysis) |
| Security | No | Yes (OWASP Top 10) |
| Architecture | Layer deps, Result pattern, CQRS, entities, migrations | Same + circular deps, anemic models |
| Quality | No | Yes (code smells, naming, testing gaps) |
| When | During development, quick verification | Before PR, as gate in /complete-task |

Use `/check-architecture` for quick feedback while coding. Use `/code-review` for the final gate before a PR.

## Integration with /complete-task

When called as an agent from `/complete-task`:
- Return only the Summary section with pass/fail status
- Include the count of critical issues
- Critical issues cause the parent workflow to pause for fixes

## Guidelines

- Be specific — include file paths and line numbers
- Be constructive — suggest fixes, don't just criticize
- Prioritize — focus on what matters most for this codebase
- Context matters — consider the feature being built
- Don't nitpick — save style debates for linters
