---
name: start-work
description: Create a feature branch from a GitHub issue and set up the working context. Use whenever starting implementation on a new issue — creates the branch, fetches requirements, and begins building automatically.
allowed-tools: Bash, Read
user-invocable: true
---

# Start Work

Create a feature branch from a GitHub issue and prepare the working context.

## Arguments

- `{issue}` — GitHub issue number (required). Pass as argument: `/start-work 42`

## Process

1. **Fetch issue details** — Read the issue title and body to understand the scope.
2. **Ensure clean working tree** — Check `git status` for uncommitted changes. Warn the user if the tree is dirty.
3. **Pull latest main** — Ensure the branch starts from the latest main.
4. **Create and checkout branch** — Use the naming convention below.
5. **Report** — Show the branch name and a summary of the issue tasks.

## Branch Naming Convention

```
{username}/{issue-number}-{short-slug}
```

Derive `{username}` from the git config `user.name` (lowercase, no spaces). Derive `{short-slug}` from the issue title (lowercase, hyphens, max 50 chars, no special characters).

Examples:
- `makigjuro/42-device-group-assignments`
- `makigjuro/15-bundle-versioning-api`

## Commands

```bash
# Fetch issue
gh issue view {issue} --json title,body,labels

# Check working tree
git status --short

# Update main and create branch
git fetch origin main
git checkout -b {branch-name} origin/main
```

## After Starting — Automatic Implementation

Once the branch is created, **do not stop**. Immediately continue with implementation:

1. **Analyze the issue** — Parse acceptance criteria, identify affected services, and determine which layers need changes (Domain, Application, Infrastructure, Endpoints, Frontend, Tests).
2. **Create a task list** — Use TodoWrite to track each implementation step derived from the issue.
3. **Implement** — Follow the layer order: Domain → Application → Infrastructure → Endpoints → Frontend → Tests. Use the appropriate scaffolding skills (`/add-entity`, `/add-command`, `/add-query`, `/add-event-handler`, `/add-migration`, `/add-feature`) where they apply. Commit logically after each meaningful change.
4. **Verify as you go** — Run tests and architecture checks between steps. Fix issues before moving on.
5. **When done** — Run `/complete-task` to go through the full completion workflow (build, lint, tests, review, PR).

**Do not ask the user what to do next.** Read the issue, plan the work, and start building. Only ask clarifying questions if the issue has genuine ambiguity that blocks implementation.
