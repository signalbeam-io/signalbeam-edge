---
name: plan-feature
description: Break down a feature into a structured plan with acceptance criteria, affected services, and implementation tasks. Use for simple features that don't need a full PRD — or when the user describes what they want to build and needs a task breakdown. For complex features needing full requirements, success metrics, and technical planning, use /prd instead.
allowed-tools: Read, Glob, Grep, Bash, WebSearch, AskUserQuestion, EnterPlanMode, ExitPlanMode
model: claude-opus-4-6
user-invocable: true
---

# Plan Feature

When the user describes a feature they want to build, produce a structured feature plan. Explore the codebase first to ground the plan in what already exists.

## Process

### Phase 0: Enter Plan Mode

**Always start by calling `EnterPlanMode` before doing anything else.** Feature planning is a planning activity — it requires codebase exploration, scope clarification, and user alignment before producing the plan. Plan mode ensures you can explore freely and get user sign-off on the approach. Exit plan mode with `ExitPlanMode` only after the plan is complete and the user has approved it.

1. **Clarify scope** — Ask the user if the feature description is ambiguous. Identify which microservice(s) are affected.
2. **Explore existing code** — Search for related entities, handlers, endpoints, and tests that the feature touches or extends.
3. **Produce the plan** — Output a structured plan using the format below.

## Plan Format

```markdown
# Feature: {Title}

## Summary
One-paragraph description of the feature and the problem it solves.

## Affected Services
- [ ] SignalBeam.DeviceManager
- [ ] BundleOrchestrator
- [ ] TelemetryProcessor
- [ ] SignalBeam.EdgeAgent
- [ ] SignalBeam.Domain (shared)
- [ ] web (frontend)

## Acceptance Criteria
- [ ] AC1: ...
- [ ] AC2: ...

## Implementation Tasks

### Domain
- [ ] {task description} — `{file path or new file}`

### Application
- [ ] {task description} — `{file path or new file}`

### Infrastructure
- [ ] {task description} — `{file path or new file}`

### Host / Endpoints
- [ ] {task description} — `{file path or new file}`

### Frontend
- [ ] {task description} — `{file path or new file}`

### Tests
- [ ] {task description} — `{file path or new file}`

## Open Questions
- Any unresolved design decisions or trade-offs to call out.

## Out of Scope
- Anything explicitly excluded from this feature.
```

## Output

After planning, report in the plan format above, then:
```
Plan complete. Next steps:
- Run `/create-tasks` to push to GitHub
- Or run `/start-work {issue}` if an issue already exists
```

## When NOT to Use

- **Complex features needing full requirements** — use `/prd` for structured discovery, success metrics, and technical planning
- **Infrastructure-only changes** — use `/infra-plan` instead
- **Bug fixes with a clear issue** — skip planning and go straight to `/start-work`

## Guidelines

- Follow the development workflow order: Domain → Application → Infrastructure → Endpoints → Tests.
- Reference existing patterns in the codebase (e.g., how devices or bundles are already implemented).
- Keep tasks small enough that each maps to roughly one file or one logical change.
- Flag when a task requires a new migration, a new NATS subject, or a new API route.
- If the feature spans multiple services, note cross-service integration points.
- After producing the plan, suggest the user run `/create-tasks` to push it to GitHub.
