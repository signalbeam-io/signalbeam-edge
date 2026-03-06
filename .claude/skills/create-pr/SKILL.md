---
name: create-pr
description: Create a pull request with a structured description linking to the GitHub issue. Use when ready to push and open a PR — auto-detects changed services/layers and generates a structured PR body with test plan.
allowed-tools: Bash, Read, Glob, Grep
user-invocable: true
---

# Create Pull Request

Create a pull request for the current branch with a structured description.

## Arguments

- `{issue}` — GitHub issue number to link (optional). If not provided, attempt to extract from the branch name.

## Process

1. **Gather context:**
   - `git status` — check for uncommitted changes, warn if dirty.
   - `git log origin/main..HEAD --oneline` — list all commits on this branch.
   - `git diff origin/main...HEAD --stat` — summary of changed files.
   - Extract issue number from branch name or argument.

2. **Analyze changes:**
   - Identify which layers were modified (Domain, Application, Infrastructure, Host, Frontend, Tests).
   - Detect affected services by checking paths against known service directories:
     ```bash
     git diff origin/main...HEAD --name-only | grep -oP 'src/\K[^/]+' | sort -u
     ```
   - Detect what was added vs changed vs removed from the diff stat.
   - Check if documentation may need updating (new endpoints, entities, or events).

3. **Push and create PR:**
   - Push the branch to origin with `-u` flag.
   - Create the PR using `gh pr create`.

## PR Body Format

```markdown
## Summary
{1-3 bullet points describing what this PR does}

Closes #{issue-number}

## Affected Services
{List only services that were actually modified, e.g.: DeviceManager, BundleOrchestrator, Frontend}

## Changes
- **Domain:** {changes or "No changes"}
- **Application:** {changes or "No changes"}
- **Infrastructure:** {changes or "No changes"}
- **Endpoints:** {changes or "No changes"}
- **Frontend:** {changes or "No changes"}
- **Tests:** {changes or "No changes"}

## Test Plan
- [ ] Unit tests pass (`dotnet test --filter "Category!=Integration"`)
- [ ] Integration tests pass (`dotnet test --filter "Category=Integration"`)
- [ ] Frontend lint clean (`npm run lint`)
- [ ] Frontend type-check clean (`npm run type-check`)
- [ ] Helm lint clean (`helm lint`)
- [ ] Terraform validate clean (`terraform validate`)
- [ ] Architecture check passes (`/check-architecture`)
- [ ] {Any feature-specific manual verification steps}

🤖 Generated with [Claude Code](https://claude.com/claude-code)
```

## Commands

```bash
# Push branch
git push -u origin HEAD

# Create PR
gh pr create --title "{title}" --body "$(cat <<'EOF'
{body}
EOF
)"
```

## Guidelines

- Keep the PR title under 70 characters, in imperative mood ("Add device group assignments", not "Added").
- Use `Closes #{issue}` to auto-close the linked issue on merge.
- If the branch has many commits, suggest the user consider squashing before creating the PR.
- Report the PR URL when done.
