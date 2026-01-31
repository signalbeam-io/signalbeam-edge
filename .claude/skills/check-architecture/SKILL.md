---
name: check-architecture
description: Verify hexagonal architecture layer rules, Result pattern usage, and coding conventions
allowed-tools: Read, Glob, Grep, Bash
---

# Architecture Check

Verify the project follows its architectural rules. Run these checks and report violations.

## 1. Layer Dependency Violations

Check that Domain layer has no forbidden references:
- Search `src/**/Domain/**/*.cs` for `using` statements referencing Infrastructure, Host, Entity Framework, ASP.NET
- Domain should only reference System.*, its own namespace, and pure abstractions

Check that Application layer doesn't reference Host:
- Search `src/**/*.Application/**/*.cs` for `using` statements referencing `*.Host`

Check that no circular project references exist in .csproj files.

## 2. Result Pattern Compliance

Search Application layer handlers for:
- Methods that `throw` exceptions for business logic (should use `Result.Failure()` instead)
- Handlers that return raw types instead of `Result<T>`
- Catch blocks that swallow exceptions without proper Result conversion

Acceptable throws: `ArgumentException` in value object constructors, `InvalidOperationException` for programmer errors.

## 3. CQRS Conventions

Verify:
- Command records are in `Application/Commands/` folders
- Query records are in `Application/Queries/` folders
- Command handlers don't use query-only repositories for writes
- Query handlers don't call write repositories

## 4. Domain Entity Conventions

Check entities in `Domain/Entities/`:
- Inherit from `Entity<TId>` or `AggregateRoot<TId>`
- Have protected parameterless constructor
- Use factory methods (static Create/Register methods) not public constructors
- Domain events raised via `RaiseDomainEvent()`

## 5. Error Code Conventions

Search for `Result.Failure` calls and verify:
- Error codes use SCREAMING_SNAKE_CASE
- Error codes are descriptive: `"DEVICE_NOT_FOUND"` not `"NOT_FOUND"`

## 6. Endpoint Conventions

Check endpoints have:
- `.WithName()`, `.WithSummary()`, `.WithOpenApi()`
- `.Produces<T>()` and `.ProducesProblem()` declarations
- Result-to-HTTP conversion via `.ToHttpResult()`

## Report Format

For each check, report:
- PASS or FAIL with count of violations
- File path and line number for each violation
- Suggested fix
