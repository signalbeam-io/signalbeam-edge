---
name: create-issue
description: Create a GitHub issue from a feature plan with labels, acceptance criteria, and implementation checklist
allowed-tools: Bash, Read, Glob
user-invocable: true
---

# Create GitHub Issue

Take the most recent feature plan from the conversation and create a GitHub issue.

## Process

1. **Extract from conversation** — Find the most recent feature plan. If none exists, ask the user to describe the feature or run `/plan-feature` first.
2. **Determine labels** — Pick labels based on affected areas:
   - `feature` for new functionality
   - `enhancement` for improvements to existing functionality
   - `bug` for defects
   - `backend` if .NET services are affected
   - `frontend` if the web app is affected
   - `infrastructure` if Helm/Terraform/CI changes are needed
   - `domain` if shared domain model changes
3. **Create the issue** — Use `gh issue create` with the structured body.

## Issue Body Format

```markdown
## Summary
{One-paragraph description}

## Acceptance Criteria
- [ ] AC1: ...
- [ ] AC2: ...

## Implementation Tasks

### Domain
- [ ] {task}

### Application
- [ ] {task}

### Infrastructure
- [ ] {task}

### Endpoints
- [ ] {task}

### Frontend
- [ ] {task}

### Tests
- [ ] {task}

## Out of Scope
- {exclusions}
```

## Commands

**Create issue with labels:**
```bash
gh issue create --title "{Feature Title}" --body "$(cat <<'EOF'
{body}
EOF
)" --label "feature,backend"
```

**Verify creation:**
```bash
gh issue view {number}
```

## After Creating

- Report the issue number and URL to the user.
- Suggest running `/start-work {issue-number}` to create a branch and begin implementation.
