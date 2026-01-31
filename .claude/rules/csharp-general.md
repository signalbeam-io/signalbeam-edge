---
paths:
  - "src/**/*.cs"
---

# C# General Rules

## Commands & Queries
- Define as `record` types (immutable): `public record RegisterDeviceCommand(Guid TenantId, string Name);`
- Handlers return `Result<T>` or `Result`, never throw exceptions for business logic
- Handler signature: `public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)`

## Error Handling
- Use `Result<T>` from `SignalBeam.Shared.Infrastructure.Results`
- Error codes in SCREAMING_SNAKE_CASE: `"DEVICE_ALREADY_EXISTS"`, `"BUNDLE_NOT_FOUND"`
- Use typed `ErrorType` enum: Validation, NotFound, Conflict, Unauthorized, Forbidden, Failure, Unexpected
- Never throw exceptions for expected business failures

## Async
- All I/O is async with `CancellationToken` propagated
- Suffix async methods with `Async`: `GetByIdAsync()`, `AddAsync()`
- Always await or return tasks, never fire-and-forget without explicit intent

## Multi-tenancy
- `TenantId` is a first-class value object, present on commands and entities
- Filter queries by tenant where applicable

## DI Registration
- Services registered as scoped: `services.AddScoped<IService, Service>()`
- Background services: `services.AddHostedService<T>()`
- Options pattern: `services.Configure<TOptions>(configuration.GetSection("Key"))`
- HTTP clients: `services.AddHttpClient<IClient, Client>(client => { client.Timeout = ...; })`

## Naming
- PascalCase for types, methods, properties
- `_camelCase` for private fields
- Interfaces prefixed with `I`
- Folders match namespace segments
