# Development Workflow

Follow this end-to-end process when building features. Each step has a corresponding skill.

## Workflow Overview

```
PRD → Issues → Branch → Implement → Verify → Review → Docs → PR
 │       │        │          │          │        │       │      │
/prd  /create   /start    /add-*    /check   /code   /docs  /complete
      -tasks    -work               -arch   -review         -task
                                    /lint
                                    /run-tests
```

For simple features without PRD:
```
Plan → Issues → Branch → Implement → ...
  │       │        │
/plan  /create   /start
-feature -tasks   -work
```

## 1. Requirements (`/prd`)

For complex features, start with a Product Requirements Document:
- Answer structured discovery questions
- Analyze existing codebase for patterns
- Generate comprehensive PRD with acceptance criteria
- Output: `docs/prd/{feature-slug}.md`

Skip for simple bug fixes or small enhancements.

## 2. Plan (`/plan-feature`)

Break down the feature into implementation tasks:
- Identify affected services and acceptance criteria
- Create tasks ordered by layer: Domain → Application → Infrastructure → Endpoints → Frontend → Tests
- Call out open questions and out-of-scope items

## 3. Track (`/create-tasks`)

Use `/create-tasks` — it automatically decides the right structure:
- **Small features** (≤ 3 tasks, single service): creates a single issue with checklist
- **Large features** (multiple tasks/services): creates an epic + individual task issues

The issue(s) become the single source of truth for scope.

## 4. Branch (`/start-work {issue-number}`)

Create a feature branch from latest main:
- Naming convention: `{username}/{issue-number}-{short-slug}`
- Always start from a clean working tree
- Print acceptance criteria as a reminder

## 5. Implement

Follow the layer order: Domain → Application → Infrastructure → Endpoints → Frontend → Tests.

Use scaffolding skills:
- `/add-entity` — New domain entity with value object ID (**includes migration**)
- `/add-command` — New CQRS command with handler and validator
- `/add-query` — New CQRS query with handler
- `/add-event-handler` — WolverineFx event handler for domain/integration events
- `/add-migration` — EF Core migration after model changes
- `/add-feature` — Frontend feature module (page, components, API service)

**IMPORTANT:** Always create an EF Core migration (`/add-migration`) immediately after adding or modifying entities, configurations, or DbSets. Never commit model changes without the corresponding migration.

Run the local environment with `/run-local` to test as you build.

Commit logically — one commit per logical change, not one giant commit.

## 6. Verify

Run verification checks during development:

| Skill | Purpose | When to Use |
|-------|---------|-------------|
| `/check-architecture` | Layer violations, Result pattern, pending migrations | Before committing |
| `/run-tests` | Unit and integration tests | After each change |
| `/lint` | Format, ESLint, Helm, Terraform | Before PR |

## 7. Complete Task (`/complete-task`)

When implementation is done, run the full completion workflow:

1. **Pre-flight** — Verify on feature branch, clean working tree
2. **Build & Lint** — Deterministic bash gates
3. **Tests** — Unit tests, then integration tests
4. **Code Review** — Subagent checks security, architecture, quality
5. **Task Check** — Subagent verifies acceptance criteria
6. **Auto-fix** — If issues found, fix and retry (max 3 iterations)
7. **Create PR** — Push and create pull request

This replaces manually running `/check-architecture`, `/lint`, `/run-tests`, and `/create-pr`.

## Infrastructure Workflow

For infrastructure-only changes (Terraform, Helm, CI/CD), use the dedicated infra workflow. This skips .NET build/tests and frontend lint entirely.

```
Plan → Issues → Branch → Implement → Verify → PR
  │       │        │          │          │      │
/infra  /create  /start    /add-*    /infra  /complete
-plan   -tasks   -work               -lint   -infra
```

### Infra Skills

| Skill | Description |
|-------|-------------|
| `/infra-plan` | Plan infrastructure changes (Terraform, Helm, CI/CD) |
| `/add-terraform-module` | Scaffold new Terraform module with Terragrunt wiring |
| `/add-helm-chart` | Scaffold new Helm chart with standard templates |
| `/infra-lint` | Lint Terraform/Helm only (fast, skips .NET/frontend) |
| `/infra-apply` | Run `terraform plan` and optionally apply |
| `/complete-infra` | Infra-specific completion: lint, validate, review, PR |

### When to Use Infra Workflow vs Standard Workflow

- **Infra workflow:** Changes only touch `infra/`, `deploy/`, or `.github/workflows/`
- **Standard workflow:** Changes touch application code (`src/`, `web/`, `tests/`)
- **Both:** If a feature needs app code + infra changes, use standard workflow with `/infra-lint` for validation

## 8. Document (`/docs`)

Keep documentation in sync with code changes:

```bash
/docs service device-manager    # Full service doc (endpoints, events, config)
/docs api device-manager        # API endpoint reference only
/docs architecture              # Update technical architecture overview
/docs domain                    # Update domain model (entities, value objects, events)
/docs runbook device-manager    # Operational runbook (health, alerts, troubleshooting)
/docs quickstart                # Update getting started guide
/docs all                       # Detect stale docs and regenerate
/docs                           # Auto-detect what needs updating
```

**When to run:**
- After adding/modifying endpoints → `/docs api {service}`
- After adding entities or events → `/docs domain`
- After changing infra/deploy → `/docs architecture`
- Before a release → `/docs all`

Output paths follow `docs/services/{service-name}/`, `docs/architecture/`, or `docs/quickstart.md`. Generated sections are marked with `<!-- BEGIN GENERATED -->` / `<!-- END GENERATED -->` so manual content is preserved on regeneration.

## 9. Diagnose (`/diagnose`)

When something goes wrong:
- Structured evidence gathering
- Hypothesis generation and testing
- Root cause analysis (5 Whys)
- Solution proposals with trade-offs

## Quick Reference

| Skill | Description |
|-------|-------------|
| `/prd` | Generate PRD through discovery |
| `/plan-feature` | Break feature into tasks (simple features) |
| `/create-tasks` | Create GitHub issues (auto: single or epic + tasks) |
| `/start-work N` | Create feature branch |
| `/add-entity` | Scaffold domain entity |
| `/add-command` | Scaffold CQRS command |
| `/add-query` | Scaffold CQRS query |
| `/add-event-handler` | Scaffold WolverineFx event handler |
| `/add-migration` | Create EF Core migration |
| `/add-feature` | Scaffold frontend feature module |
| `/run-local` | Start local dev environment (Aspire) |
| `/check-architecture` | Verify architecture rules |
| `/run-tests` | Run unit/integration tests |
| `/lint` | Run all linters |
| `/code-review` | Review code changes |
| `/task-check` | Verify acceptance criteria |
| `/complete-task` | Full completion workflow |
| `/create-pr` | Create pull request |
| `/docs` | Generate/update documentation from code |
| `/diagnose` | Investigate problems |
| `/infra-plan` | Plan infrastructure changes |
| `/add-terraform-module` | Scaffold Terraform module |
| `/add-helm-chart` | Scaffold Helm chart |
| `/infra-lint` | Lint Terraform/Helm only |
| `/infra-apply` | Terraform plan/apply |
| `/complete-infra` | Infra completion workflow |

## Subagents

These specialized agents run in isolated contexts:

| Subagent | Purpose |
|----------|---------|
| `reviewer` | Security, architecture, quality review |
| `verifier` | Acceptance criteria verification |
| `investigator` | Evidence gathering for diagnosis |
| `analyzer` | Codebase analysis for new features |

## Pre-PR Checklist

Before `/complete-task` or `/create-pr`:
- [ ] All acceptance criteria addressed
- [ ] Architecture check passes (including no pending migrations)
- [ ] All unit tests pass
- [ ] All integration tests pass (if applicable)
- [ ] .NET format is clean
- [ ] Frontend lint is clean
- [ ] Frontend type-check is clean
- [ ] Helm charts lint clean
- [ ] Terraform validates and format is clean
- [ ] No uncommitted changes
- [ ] Commits are logically organized
