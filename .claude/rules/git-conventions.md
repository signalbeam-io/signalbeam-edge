---
paths:
  - "**"
---

# Git Conventions

## Branch Naming

Format: `{username}/{issue-number}-{short-slug}`

Examples:
- `makigjuro/166-aks-cluster-services`
- `makigjuro/247-real-time-foundation-sse-cdc-websocket`

Legacy branches used `feature/` prefix — new branches must use the `{username}/` format.

## Commit Messages

Format: lowercase imperative, no period at end

```
{verb} {what was done}
```

**Verbs:**
- `add` — new feature or file (not "added", not "adds")
- `fix` — bug fix
- `update` — enhancement to existing feature
- `remove` — delete code or feature
- `refactor` — restructure without behaviour change
- `rename` — rename files, variables, or types
- `move` — relocate files between directories
- `enforce` — add or tighten a rule/constraint
- `address` — respond to review feedback

**Examples from this repo:**
```
add DeviceManager Helm chart with environment-specific values
fix: address review feedback on SSE connection manager and endpoint
deploy NATS with JetStream to AKS via Terraform + Helm
address review findings: resource limits, labels, kubeconfig parsing
enforce mandatory EF Core migrations in rules, skills, and workflow
```

**Rules:**
- First line under 72 characters
- No capitalization of first word (lowercase)
- No trailing period
- One logical change per commit — not one giant commit per feature
- Reference issue number in the PR, not in every commit message

## Protected Branches

- Never commit directly to `main` — always use feature branches
- Never force-push to `main`
- Merge via pull request only

## Staging

- Stage specific files by name, not `git add -A` or `git add .`
- Never commit `.env`, `appsettings.*.json` with secrets, or `**/bin/` / `**/obj/` directories
- Review `git diff --cached` before committing

## PR Conventions

- PR title: imperative mood, under 70 characters (matches the primary commit)
- Link to GitHub issue with `Closes #{number}`
- One PR per issue — don't bundle unrelated changes
