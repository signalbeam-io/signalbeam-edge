# Development Workflow

Follow this end-to-end process when building features. Each step has a corresponding skill.

## 1. Plan (`/plan-feature`)
- Break down the feature into domain, application, infrastructure, endpoint, frontend, and test tasks.
- Identify affected services and acceptance criteria.
- Call out open questions and out-of-scope items.

## 2. Track (`/create-issue`)
- Create a GitHub issue from the plan with labels and a checklist.
- The issue becomes the single source of truth for scope.

## 3. Branch (`/start-work {issue-number}`)
- Create a feature branch from latest main using the naming convention: `{username}/{issue-number}-{short-slug}`.
- Always start from a clean working tree.

## 4. Implement
- Follow the layer order: Domain → Application → Infrastructure → Endpoints → Frontend → Tests.
- Use `/add-entity`, `/add-command`, `/add-query` to scaffold code following project conventions.
- Commit logically — one commit per logical change, not one giant commit.

## 5. Verify Architecture (`/check-architecture`)
- Run before creating a PR to catch layer violations, Result pattern issues, and convention drift.

## 6. Verify Tests (`/run-tests`)
- Run unit tests first, then integration tests.
- All tests must pass before creating a PR.

## 7. Lint (`/lint`)
- Run all linters: `dotnet format`, `npm run lint`, `helm lint`, `terraform validate`, `terraform fmt -check`.
- Fix all violations before creating a PR.

## 8. Pull Request (`/create-pr`)
- Push the branch and create a PR with a structured description.
- Link to the issue with `Closes #N` for auto-close on merge.
- Include a test plan checklist in the PR body.

## Pre-PR Checklist
Before running `/create-pr`, verify:
- [ ] Architecture check passes
- [ ] All unit tests pass
- [ ] All integration tests pass (if applicable)
- [ ] .NET format is clean
- [ ] Frontend lint is clean
- [ ] Frontend type-check is clean
- [ ] Helm charts lint clean
- [ ] Terraform validates and format is clean
- [ ] No uncommitted changes
- [ ] Commits are logically organized
