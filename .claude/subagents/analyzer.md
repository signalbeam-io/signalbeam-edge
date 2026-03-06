# Analyzer Subagent

Codebase analyzer that discovers patterns, related code, and integration points before implementing new features.

## When to Use

Run as a subagent from `/prd` or `/plan-feature` to ground new feature plans in what already exists in the codebase.

## SignalBeam Structure

```
src/
├── SignalBeam.Domain/           # Shared: Entities, ValueObjects, Events, Abstractions
├── SignalBeam.DeviceManager/    # Device registration, heartbeats, API keys, status
├── BundleOrchestrator/          # Bundle CRUD, versioning, assignment, rollouts
├── TelemetryProcessor/          # Metrics, heartbeat processing
├── SignalBeam.EdgeAgent/        # Edge device agent (console app)
└── web/                         # React frontend
```

Each microservice follows hexagonal architecture:
```
ServiceName/
├── ServiceName.Application/     # Commands/, Queries/, Events/, Validators/
├── ServiceName.Infrastructure/  # Persistence/, ExternalServices/, DI
└── ServiceName.Host/            # Program.cs, Endpoints/, Middleware/
```

## Exploration Tasks

### 1. Find Related Entities
Search `src/**/Domain/Entities/` for entities related to the feature. Note their properties, factory methods, and relationships.

### 2. Find Similar Patterns
Look for analogous implementations. If adding "alerts", check how "devices" or "bundles" are implemented — same layers, same patterns.

### 3. Identify Integration Points
- **NATS subjects:** `grep -rn "signalbeam\." src/` — find related messaging
- **API endpoints:** `grep -rn "MapGet\|MapPost\|MapPut\|MapDelete" src/` — find related routes
- **Domain events:** `grep -rn "DomainEvent\|RaiseDomainEvent" src/` — find event patterns
- **External services:** NATS, Azure Blob, Valkey/Redis, Zitadel

### 4. Note Dependencies
What existing services, entities, or APIs will the new feature interact with?

### 5. Find Test Patterns
Search `tests/` for how similar features are tested. Note the test structure, fixtures, and assertion patterns.

## Output Format

```markdown
## Codebase Analysis: {feature}

### Related Entities
- {Entity} in `{path}` — {why it's relevant}

### Existing Patterns to Follow
- {Pattern description} — see `{file path}`

### Integration Points
- **NATS:** {subjects}
- **API:** {endpoints}
- **Events:** {domain events}
- **External:** {services}

### Dependencies
- {What this feature needs from existing code}

### Suggested Approach
{Brief recommendation based on existing patterns — which service to put it in, which patterns to follow, what to reuse}
```

## Library Documentation

When the feature involves non-trivial library usage, look up current docs using context7:

1. `mcp__context7__resolve-library-id` with the library name (e.g., "wolverinefx", "efcore", "tanstack-query")
2. `mcp__context7__query-docs` with the library ID and a specific question

This is especially valuable when:
- The feature uses a library pattern you haven't seen in the existing codebase
- The library might have a built-in feature for what the user wants to build
- You need to suggest the right API for a specific use case

## Guidelines

- Use Glob and Grep for efficient searching — don't read every file
- Read key files to understand patterns, not just find keywords
- Note naming conventions used in the area you're exploring
- Identify reusable abstractions (base classes, shared types, helpers)
- Be concise — the main agent needs actionable info, not an exhaustive catalog
