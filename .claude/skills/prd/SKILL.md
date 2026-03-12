---
name: prd
description: Generate a Product Requirements Document through guided discovery questions and codebase analysis. Use for complex features that need structured requirements, success metrics, and technical planning before implementation.
allowed-tools: Bash, Read, Glob, Grep, Task, AskUserQuestion, Write, EnterPlanMode, ExitPlanMode
user-invocable: true
---

# PRD Generator

Create a detailed, engineer-focused Product Requirements Document through structured discovery. The PRD is optimized for AI-assisted development and integrates with the `/plan-feature` workflow.

## Arguments

- `{name}` — Short feature name for the file (optional, will prompt if not provided)
- `--minimal` — Use minimal template for simple features
- `--skip-analysis` — Skip codebase analysis (faster, for greenfield features)

## Process

### Phase 0: Enter Plan Mode

**Always start by calling `EnterPlanMode` before doing anything else.** PRD generation is a planning activity — it requires codebase exploration, discovery questions, and user alignment before producing output. Plan mode ensures you can explore freely and get user sign-off on the approach before writing the PRD file. Exit plan mode with `ExitPlanMode` only after Phase 4 validation is complete and the user has approved the PRD content.

### Phase 1: Discovery Questions

Ask the user these questions using AskUserQuestion. Group related questions together.

**Problem & Users**
1. What problem are we solving? What's the business impact if we don't solve it?
2. Who are the primary users? (e.g., platform admins, device operators, edge devices)
3. What's their current workaround, if any?

**Success & Scope**
4. How will we measure success? What metrics matter?
5. What's explicitly OUT of scope for this feature?
6. Are there related features we should be aware of but not implement now?

**Technical Context**
7. Which services will this touch? (DeviceManager, BundleOrchestrator, TelemetryProcessor, EdgeAgent, Frontend)
8. Are there external system integrations? (NATS, Azure Blob, Zitadel, etc.)
9. Any specific performance requirements? (latency, throughput, scale)

**Constraints**
10. Timeline or deadline constraints?
11. Backward compatibility requirements?
12. Security or compliance considerations?

### Phase 2: Codebase Analysis (unless --skip-analysis)

Use the `analyzer` agent to analyze the existing codebase:

```
Analyze the SignalBeam Edge codebase for implementing "{feature name}".

Find:
1. Related entities in src/**/Domain/Entities/
2. Existing commands/queries that might be extended
3. Similar patterns already implemented
4. API endpoints that might need changes
5. Frontend components that might be affected
6. Relevant domain events
7. Integration points (NATS subjects, external services)

Output:
## Codebase Analysis

### Related Entities
- {Entity} in {path} — {relevance}

### Existing Patterns to Follow
- {Pattern description} — see {file}

### Integration Points
- {Service/Subject/Endpoint} — {how it relates}

### Suggested Approach
{Brief recommendation based on existing patterns}
```

### Phase 3: Generate PRD

Create the PRD file at `docs/prd/{feature-slug}.md`:

```markdown
# PRD: {Feature Name}

> Generated: {date}
> Author: Claude Code + {user}
> Status: Draft

## 1. Executive Summary

{One paragraph: what, why, who benefits}

## 2. Problem Statement

### Current State
{What exists today, pain points}

### Business Impact
{Cost of not solving, opportunity if solved}

### User Impact
{How users are affected}

## 3. Goals & Success Metrics

### Goals
- Primary: {main goal}
- Secondary: {supporting goals}

### Success Metrics
| Metric | Current | Target | How to Measure |
|--------|---------|--------|----------------|
| {metric} | {baseline} | {target} | {measurement method} |

### Non-Goals
- {Explicitly out of scope items}

## 4. User Stories

### {User Persona 1}
- As a {role}, I want {capability} so that {benefit}
  - Acceptance: {testable criterion}

### {User Persona 2}
- As a {role}, I want {capability} so that {benefit}
  - Acceptance: {testable criterion}

## 5. Functional Requirements

| ID | Requirement | Priority | Acceptance Criteria |
|----|-------------|----------|---------------------|
| FR-1 | {requirement} | Must | {testable criteria} |
| FR-2 | {requirement} | Should | {testable criteria} |
| FR-3 | {requirement} | Could | {testable criteria} |

## 6. Technical Considerations

### Affected Services
- [ ] SignalBeam.DeviceManager — {changes}
- [ ] BundleOrchestrator — {changes}
- [ ] TelemetryProcessor — {changes}
- [ ] SignalBeam.EdgeAgent — {changes}
- [ ] SignalBeam.Domain (shared) — {changes}
- [ ] web (frontend) — {changes}

### Data Model Changes
{New entities, modified entities, migrations needed}

### API Changes
| Endpoint | Method | Change Type | Description |
|----------|--------|-------------|-------------|
| {path} | {verb} | New/Modified | {description} |

### NATS Subjects
{New or modified subjects}

### External Integrations
{Third-party services, APIs}

### Performance Considerations
{Expected load, latency requirements, caching strategy}

### Security Considerations
{Auth requirements, data sensitivity, audit needs}

## 7. Dependencies

### Upstream
- {Features/services this depends on}

### Downstream
- {Features/services that will depend on this}

### External
- {Third-party dependencies}

## 8. Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| {risk} | {H/M/L} | {H/M/L} | {mitigation strategy} |

## 9. Out of Scope

- {Item 1} — will be addressed in {future work}
- {Item 2} — explicitly excluded because {reason}

## 10. Open Questions

- [ ] {Question that needs resolution}
- [ ] {Question that needs resolution}

## 11. Task Breakdown Hints

These hints help AI task generators break down the work:

### Domain Layer
- {Entity/ValueObject to create or modify}

### Application Layer
- {Commands to add}
- {Queries to add}
- {Event handlers to add}

### Infrastructure Layer
- {Repository changes}
- {External service clients}
- {Migrations}

### Endpoints
- {API routes to add/modify}

### Frontend
- {Pages/components to add/modify}

### Tests
- {Key test scenarios}

---

## Appendix: Codebase Analysis

{Output from Phase 2}
```

### Phase 4: Validation

Run these automated checks on the generated PRD:

1. **Completeness** — All sections filled (warn on empty sections)
2. **Measurability** — Success metrics have numbers, not vague terms
3. **Testability** — Acceptance criteria are specific and testable
4. **No vague language** — Flag words like "fast", "secure", "easy" without metrics
5. **Scope clarity** — Out of scope section is not empty

Report validation results:
```
## PRD Validation

- Completeness: {PASS | WARN: missing sections}
- Measurability: {PASS | WARN: vague metrics}
- Testability: {PASS | WARN: untestable criteria}
- Language: {PASS | WARN: vague terms found}
- Scope: {PASS | WARN: no exclusions defined}

Overall: {PASS | NEEDS REVIEW}
```

### Phase 5: Next Steps

After generating the PRD, suggest:

```
## Next Steps

1. Review and refine the PRD with stakeholders
2. Resolve open questions
3. Run `/plan-feature` to create implementation tasks
4. Run `/create-tasks` to track in GitHub

PRD saved to: docs/prd/{feature-slug}.md
```

## Minimal Template (--minimal)

For simple features, use a condensed format:

```markdown
# PRD: {Feature Name}

## Problem
{What and why in 2-3 sentences}

## Solution
{How we'll solve it in 2-3 sentences}

## Requirements
- [ ] {Requirement 1 with acceptance criteria}
- [ ] {Requirement 2 with acceptance criteria}

## Technical Notes
- Services: {affected services}
- API: {new/changed endpoints}
- Data: {model changes}

## Out of Scope
- {exclusions}
```

## When NOT to Use

- **Simple bug fixes or small enhancements** — skip directly to `/create-tasks` or `/start-work`
- **Features with ≤ 3 tasks in a single service** — use `/plan-feature` for a lighter-weight breakdown
- **Infrastructure-only changes** — use `/infra-plan` instead

## Guidelines

- PRDs are living documents — they can be updated as understanding evolves
- Focus on WHAT and WHY, leave HOW to `/plan-feature`
- Every requirement should be testable
- When in doubt, add it to "Open Questions" rather than assuming
- Link to existing documentation, don't duplicate it
