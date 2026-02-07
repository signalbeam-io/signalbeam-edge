---
name: plan-feature
description: Break down a feature into a structured plan with acceptance criteria, affected services, and implementation tasks
allowed-tools: Read, Glob, Grep, Bash, WebSearch
user-invocable: true
---

# Plan Feature

When the user describes a feature they want to build, produce a structured feature plan. Explore the codebase first to ground the plan in what already exists.

## Process

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

## Guidelines

- Follow the development workflow order: Domain → Application → Infrastructure → Endpoints → Tests.
- Reference existing patterns in the codebase (e.g., how devices or bundles are already implemented).
- Keep tasks small enough that each maps to roughly one file or one logical change.
- Flag when a task requires a new migration, a new NATS subject, or a new API route.
- If the feature spans multiple services, note cross-service integration points.
- After producing the plan, suggest the user run `/create-tasks` to push it to GitHub.
