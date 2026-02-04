---
name: start-work
description: Create a feature branch from a GitHub issue and set up the working context
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

## After Starting

- Print the issue acceptance criteria as a checklist so the user knows what to build.
- Remind the user of the development workflow: Domain → Application → Infrastructure → Endpoints → Tests.
