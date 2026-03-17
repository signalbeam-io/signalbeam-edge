---
name: check-architecture
description: Verify hexagonal architecture layer rules, Result pattern usage, and coding conventions. Use as a quick local check during development — fast and focused on architecture rules only. For full security + quality review, use /code-review instead.
allowed-tools: Read, Glob, Grep, Bash, mcp__signalbeam-validator__validate_all_layers, mcp__signalbeam-validator__check_all_migrations, mcp__signalbeam-validator__check_result_pattern
user-invocable: true
---

# Architecture Check

Verify the project follows its architectural rules. Run automated arch tests first, then supplement with manual checks.

## 1. Run NetArchTest.Rules Tests

First, run any architecture tests in the test suite:

```bash
dotnet test src/SignalBeam.sln --filter "FullyQualifiedName~Arch" --no-restore
```

If no arch tests exist, proceed with manual checks below.

## 2. Layer Dependency Violations

Call `mcp__signalbeam-validator__validate_all_layers` (no parameters). It checks all microservices in one call and returns structured JSON:

```json
{ "results": { "DeviceManager": { "passed": true }, "BundleOrchestrator": { "passed": false, "violations": [...] } }, "allPassed": false }
```

If `allPassed` is `false`, report each violation with the file path and offending `using` statement.

## 3. Result Pattern Compliance

For each changed handler file in `src/**/Application/**/\*Handler.cs`, call `mcp__signalbeam-validator__check_result_pattern` with the file path. It flags thrown business exceptions that should return `Result<T>` instead.

Acceptable throws (the tool already excludes these): `ArgumentException` in value object constructors, `InvalidOperationException` for programmer errors.

If no handler files changed, skip this check.

## 4. CQRS Conventions

Verify:
- Command records are in `Application/Commands/` folders
- Query records are in `Application/Queries/` folders
- Command handlers don't use query-only repositories for writes
- Query handlers don't call write repositories

## 5. Domain Entity Conventions

Check entities in `Domain/Entities/`:
- Inherit from `Entity<TId>` or `AggregateRoot<TId>`
- Have protected parameterless constructor
- Use factory methods (static Create/Register methods) not public constructors
- Domain events raised via `RaiseDomainEvent()`

## 6. Error Code Conventions

Search for `Result.Failure` calls and verify:
- Error codes use SCREAMING_SNAKE_CASE
- Error codes are descriptive: `"DEVICE_NOT_FOUND"` not `"NOT_FOUND"`

## 7. Endpoint Conventions

Check endpoints have:
- `.WithName()`, `.WithSummary()`, `.WithOpenApi()`
- `.Produces<T>()` and `.ProducesProblem()` declarations
- Result-to-HTTP conversion via `.ToHttpResult()`

## 8. Pending EF Core Migrations

Call `mcp__signalbeam-validator__check_all_migrations` (no parameters). It checks all microservices in one call and returns structured JSON:

```json
{ "results": { "DeviceManager": { "hasPending": false }, ... }, "anyPending": false }
```

Any service with `hasPending: true` is a **FAIL** — entity/configuration changes must always have a corresponding migration.

## Report Format

```markdown
## Architecture Check Results

### Automated Tests
{PASS | FAIL | SKIPPED (no arch tests found)}

### Manual Checks
| # | Check | Status | Violations |
|---|-------|--------|------------|
| 1 | Layer dependencies | {PASS/FAIL} | {count} |
| 2 | Result pattern | {PASS/FAIL} | {count} |
| 3 | CQRS conventions | {PASS/FAIL} | {count} |
| 4 | Domain entities | {PASS/FAIL} | {count} |
| 5 | Error codes | {PASS/FAIL} | {count} |
| 6 | Endpoints | {PASS/FAIL} | {count} |
| 7 | Pending migrations | {PASS/FAIL} | {count} |

### Violations
- {file}:{line} — {description} — **Fix:** {suggestion}

### Summary: {PASS | FAIL}
```

## Relationship to /code-review

This skill is the **fast, local check** — run it during development for quick feedback. For the **thorough PR-level review** (security, quality, test gaps), use `/code-review` or let `/complete-task` run it as an agent.

## Related Skills

- After fixing violations, run `/run-tests` to verify nothing broke
- Use `/add-entity`, `/add-command`, `/add-query` to scaffold code that follows conventions
