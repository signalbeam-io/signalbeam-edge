---
name: add-command
description: Scaffold a new CQRS command with handler, validator, and POST endpoint following project conventions
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
---

# Add CQRS Command

When the user asks to add a new command, scaffold the following files in the correct microservice. Ask which service if ambiguous.

## 1. Command Record (`Application/Commands/{CommandName}.cs`)

```csharp
namespace SignalBeam.{Service}.Application.Commands;

public record {Name}Command({parameters});
```

## 2. Response Record (same file or `Application/Contracts/`)

```csharp
public record {Name}Response({response fields});
```

## 3. Handler (`Application/Commands/{Name}Handler.cs`)

```csharp
namespace SignalBeam.{Service}.Application.Commands;

public class {Name}Handler
{
    // Constructor-inject repositories and services

    public async Task<Result<{Name}Response>> Handle({Name}Command command, CancellationToken cancellationToken)
    {
        // 1. Validate business rules
        // 2. Create/modify domain entities via factory methods or aggregate methods
        // 3. Persist via repository
        // 4. Return Result.Success or Result.Failure with SCREAMING_SNAKE error code
    }
}
```

## 4. Validator (`Application/Validators/{Name}Validator.cs`)

```csharp
namespace SignalBeam.{Service}.Application.Validators;

public class {Name}Validator : AbstractValidator<{Name}Command>
{
    public {Name}Validator()
    {
        // RuleFor(x => x.Field).NotEmpty().WithErrorCode("ERROR_CODE");
    }
}
```

## 5. Endpoint (add to existing `Host/Endpoints/{Domain}Endpoints.cs`)

```csharp
group.MapPost("/{route}", {Name})
    .WithName("{Name}")
    .WithSummary("...")
    .WithDescription("...")
    .Produces<{Name}Response>(StatusCodes.Status201Created)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status409Conflict);

private static async Task<IResult> {Name}(
    [FromBody] {Name}Request request,
    [FromServices] {Name}Handler handler,
    CancellationToken cancellationToken)
{
    var command = new {Name}Command(...);
    var result = await handler.Handle(command, cancellationToken);
    return result.ToHttpResult();
}
```

## 6. DI Registration

Register the handler in the service's `AddInfrastructure` or `Program.cs`:
```csharp
services.AddScoped<{Name}Handler>();
```

## Checklist
- [ ] Command record is immutable
- [ ] Handler returns `Result<T>`, no thrown exceptions for business logic
- [ ] Error codes are SCREAMING_SNAKE_CASE
- [ ] Validator uses FluentValidation
- [ ] Endpoint has OpenAPI metadata (WithName, WithSummary, Produces)
- [ ] Handler registered in DI
- [ ] CancellationToken propagated through all async calls

## Related Skills

- `/add-entity` if the command creates a new entity type
- `/add-event-handler` to react to domain events raised by this command
- `/add-query` if you also need a read endpoint for the same resource
