---
name: create-tasks
description: Create GitHub issues from a feature plan or PRD — automatically determines single issue vs epic + task issues
allowed-tools: Bash, Read, Glob, AskUserQuestion
user-invocable: true
---

# Create GitHub Tasks

Analyze a feature plan or PRD and create the appropriate GitHub issue structure. **Automatically decides** whether to create a single issue or an epic with multiple task issues based on scope and complexity.

## Arguments

- `{prd-path}` — Path to PRD file (optional, will search `docs/prd/` or use conversation context)
- `--dry-run` — Show what would be created without actually creating issues
- `--epic-only` — Create only the epic issue, not individual tasks
- `--no-epic` — Create only task issues, no parent epic

## Decision Logic: Single vs Multiple Issues

**Create a SINGLE issue when:**
- Feature has ≤ 3 implementation tasks
- All tasks affect the same service/layer
- Work is small enough for one PR (roughly < 1 day of work)
- No PRD exists — just a simple feature plan from conversation

**Create an EPIC + task issues when:**
- Feature has > 3 implementation tasks
- Tasks span multiple services or layers
- A PRD exists with structured sections
- Tasks could be assigned to different people or done in separate PRs
- `--epic-only` or `--no-epic` flags override this logic

## Process

### Step 1: Find the Plan

1. If path provided, read that file
2. Else search `docs/prd/*.md` for most recent PRD
3. Else look for feature plan in conversation context
4. If nothing found, ask user to describe the feature or run `/plan-feature` or `/prd` first

### Step 2: Parse & Classify

Extract from the plan:
- **Title**: Feature name
- **Summary**: Description
- **Acceptance Criteria**: All AC items
- **Tasks**: Implementation tasks grouped by layer
- **Out of Scope**: Exclusions
- **Labels**: Derive from affected services (see Label Mapping below)

Apply the decision logic above to determine: **single issue** or **epic + tasks**.

### Step 3: Present Plan for Approval

Show the user what will be created:

```
## Issue Creation Plan

Mode: {Single Issue | Epic + N Task Issues}
Reason: {brief explanation of why this mode was chosen}

### {Single Issue title OR Epic title}
Labels: {labels}

### Tasks ({count})
1. {task description}
2. {task description}
...

Create? [Yes / Modify / Cancel]
```

### Step 4a: Single Issue Path

Create one issue with all tasks as a checklist:

```bash
gh issue create --title "{Feature Title}" --label "{labels}" --body "$(cat <<'EOF'
## Summary
{description}

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

---
🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Only include layer sections that have tasks. Skip empty sections.

### Step 4b: Epic + Tasks Path

**Create the epic:**

```bash
gh issue create \
  --title "Epic: {Feature Title}" \
  --label "epic,feature,{labels}" \
  --body "$(cat <<'EOF'
## Summary
{summary}

## Acceptance Criteria
{all AC items as checkboxes}

## Task Issues
<!-- Links added after tasks are created -->

## Out of Scope
{exclusions}

## References
- PRD: {prd-path if applicable}

---
🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

**Create each task issue:**

```bash
gh issue create \
  --title "[{Layer}] {Task Title}" \
  --label "{layer-label},{service-labels}" \
  --body "$(cat <<'EOF'
## Parent Epic
#{epic-number}

## Task
{task description}

## Acceptance Criteria
{relevant AC items}

## Files to Modify
- `{file path}`

## Implementation Notes
{hints from PRD}

---
🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

**Update epic with task links:**

```bash
gh issue edit {epic-number} --body "$(cat <<'EOF'
{updated body with - [ ] #{taskN} — {title} links}
EOF
)"
```

## Label Mapping

| Layer/Service | Labels |
|---------------|--------|
| Domain | `domain`, `backend` |
| Application | `application`, `backend` |
| Infrastructure | `infrastructure`, `backend` |
| Endpoints | `api`, `backend` |
| Frontend | `frontend` |
| Tests | `testing` |
| DeviceManager | `device-manager` |
| BundleOrchestrator | `bundle-orchestrator` |
| TelemetryProcessor | `telemetry-processor` |
| IdentityManager | `identity-manager` |
| AppHost | `infrastructure`, `aspire` |
| Helm/Terraform | `infrastructure`, `devops` |

General labels:
- `feature` for new functionality
- `enhancement` for improvements
- `bug` for defects

## Task Title Conventions

Keep titles under 70 characters. Use `[Layer]` prefix for multi-issue mode:

| Description | Title |
|-------------|-------|
| "Add machine user to Zitadel init config" | `[Infrastructure] Add Zitadel machine user init config` |
| "Remove hardcoded audience from DeviceManager" | `[DeviceManager] Use dynamic JWT audience` |

## Grouping Strategy

Group related tasks into single issues when:
- They modify the same file
- They're logically atomic (must be done together)
- They're trivial (< 10 lines each)

Split into separate issues when:
- Tasks can be done independently
- Different reviewers might handle them
- They touch different services

## Output Format

### Single Issue Output
```markdown
## Issue Created

- #{number}: {title}
  URL: {url}

## Next Steps
Run `/start-work #{number}` to begin implementation.
```

### Epic + Tasks Output
```markdown
## Issues Created

### Epic
- #{number}: {title} — {url}

### Tasks ({count})
| # | Issue | Title | Labels |
|---|-------|-------|--------|
| 1 | #{n1} | {title1} | {labels} |
| 2 | #{n2} | {title2} | {labels} |

## Next Steps
1. Run `/start-work #{first-task}` to begin implementation
2. Close tasks as completed; epic tracks overall progress
```

## Error Handling

- **gh not authenticated**: Prompt user to run `gh auth login`
- **Label doesn't exist**: Create label or skip with warning
- **Rate limit**: Pause and retry with backoff
- **Partial failure**: Report which issues were created, which failed
