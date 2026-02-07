---
name: check-architecture
description: Verify hexagonal architecture layer rules, Result pattern usage, and coding conventions
allowed-tools: Read, Glob, Grep, Bash
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

Check that Domain layer has no forbidden references:
- Search `src/**/Domain/**/*.cs` for `using` statements referencing Infrastructure, Host, Entity Framework, ASP.NET
- Domain should only reference System.*, its own namespace, and pure abstractions

Check that Application layer doesn't reference Host:
- Search `src/**/*.Application/**/*.cs` for `using` statements referencing `*.Host`

Check that no circular project references exist in .csproj files.

## 3. Result Pattern Compliance

Search Application layer handlers for:
- Methods that `throw` exceptions for business logic (should use `Result.Failure()` instead)
- Handlers that return raw types instead of `Result<T>`
- Catch blocks that swallow exceptions without proper Result conversion

Acceptable throws: `ArgumentException` in value object constructors, `InvalidOperationException` for programmer errors.

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

### Violations
- {file}:{line} — {description} — **Fix:** {suggestion}

### Summary: {PASS | FAIL}
```

## Related Skills

- After fixing violations, run `/run-tests` to verify nothing broke
- Use `/add-entity`, `/add-command`, `/add-query` to scaffold code that follows conventions
