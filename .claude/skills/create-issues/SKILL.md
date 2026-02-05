---
name: create-issues
description: Create multiple GitHub issues from a PRD or feature plan, with an epic issue linking all tasks
allowed-tools: Bash, Read, Glob, AskUserQuestion
user-invocable: true
---

# Create GitHub Issues

Parse a PRD or feature plan and create multiple GitHub issues: one epic/parent issue for the overall feature, plus individual task issues for each implementation item.

## Arguments

- `{prd-path}` — Path to PRD file (optional, will search `docs/prd/` or use conversation context)
- `--dry-run` — Show what would be created without actually creating issues
- `--epic-only` — Create only the epic issue, not individual tasks
- `--no-epic` — Create only task issues, no parent epic

## Process

### Step 1: Find PRD or Plan

1. If path provided, read that file
2. Else search `docs/prd/*.md` for most recent PRD
3. Else look for feature plan in conversation context
4. If nothing found, ask user to provide path or run `/prd` first

### Step 2: Parse Structure

Extract from PRD/Plan:
- **Title**: Feature name
- **Summary**: Executive summary or first paragraph
- **Acceptance Criteria**: All AC items
- **Tasks**: Implementation tasks grouped by layer
- **Out of Scope**: Exclusions
- **Labels**: Derive from affected services

### Step 3: Plan Issue Structure

Present the issue structure to user for approval:

```
## Issue Creation Plan

### Epic Issue
Title: {Feature Title}
Labels: epic, feature, {derived labels}

### Task Issues ({count})
1. [{layer}] {task title} — Labels: {labels}
2. [{layer}] {task title} — Labels: {labels}
...

Create these issues? [Yes / Modify / Cancel]
```

### Step 4: Create Epic Issue

```bash
gh issue create \
  --title "Epic: {Feature Title}" \
  --label "epic,feature,{labels}" \
  --body "$(cat <<'EOF'
## Summary
{summary from PRD}

## Acceptance Criteria
{all AC items as checkboxes}

## Task Issues
<!-- Links will be added as tasks are created -->
- [ ] #{task1} — {task1 title}
- [ ] #{task2} — {task2 title}
...

## Out of Scope
{exclusions}

## References
- PRD: {prd-path if applicable}

---
🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

### Step 5: Create Task Issues

For each implementation task, create an issue:

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
{relevant AC items for this task}

## Files to Modify
- `{file path}`

## Implementation Notes
{any hints from PRD}

---
🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

### Step 6: Update Epic with Task Links

After all tasks are created, update the epic issue body to include links:

```bash
gh issue edit {epic-number} --body "$(cat <<'EOF'
{updated body with task issue numbers}
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

## Task Title Conventions

Transform task descriptions into concise titles:

| Task Description | Issue Title |
|------------------|-------------|
| "Add machine user to Zitadel init config" | `[Infrastructure] Add Zitadel machine user init config` |
| "Remove PAT authentication requirement" | `[ZitadelSetup] Remove PAT auth requirement` |
| "Remove hardcoded audience from DeviceManager" | `[DeviceManager] Use dynamic JWT audience` |

Keep titles under 70 characters.

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

```markdown
## Issues Created

### Epic
- #{number}: {title}
  URL: {url}

### Tasks ({count})
| # | Issue | Title | Labels |
|---|-------|-------|--------|
| 1 | #{n1} | {title1} | {labels} |
| 2 | #{n2} | {title2} | {labels} |
...

## Next Steps
1. Assign issues to team members
2. Run `/start-work #{first-task}` to begin implementation
3. Close tasks as completed; epic auto-closes when all tasks done

## Quick Start
```bash
/start-work {first-task-number}
```
```

## Dry Run Output

When `--dry-run` is specified:

```markdown
## Dry Run: Issues That Would Be Created

### Epic
Title: {title}
Labels: {labels}
Body preview:
> {first 200 chars of body}...

### Tasks
1. **[{Layer}] {Title}**
   Labels: {labels}
   Body preview:
   > {first 100 chars}...

---
Run without --dry-run to create these issues.
```

## Error Handling

- **gh not authenticated**: Prompt user to run `gh auth login`
- **Label doesn't exist**: Create label or skip with warning
- **Rate limit**: Pause and retry with backoff
- **Partial failure**: Report which issues were created, which failed

## Example

Given PRD with tasks:
```
### Infrastructure
- [ ] Add machine user to Zitadel init config — `src/SignalBeam.AppHost/Program.cs`

### ZitadelSetup Service
- [ ] Remove PAT authentication requirement — `src/SignalBeam.ZitadelSetup/Program.cs`
- [ ] Add client credentials authentication — `src/SignalBeam.ZitadelSetup/Program.cs`
```

Creates:
1. **Epic**: `Epic: Zitadel Auto-Bootstrap for Aspire`
2. **Task**: `[AppHost] Add Zitadel machine user init config`
3. **Task**: `[ZitadelSetup] Replace PAT auth with client credentials` (grouped 2 related tasks)

## Guidelines

- Keep epic focused on the WHAT and WHY
- Keep task issues focused on the HOW
- One task = one logical commit or PR
- Link related issues using `Related to #N` in body
- Use `Blocked by #N` for dependencies
- Epic should be closeable when all tasks complete
