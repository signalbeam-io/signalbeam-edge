---
name: add-query
description: Scaffold a new CQRS query with handler and GET endpoint following project conventions
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
---

# Add CQRS Query

When the user asks to add a new query, scaffold the following files in the correct microservice. Ask which service if ambiguous.

## 1. Query Record (`Application/Queries/{QueryName}.cs`)

```csharp
namespace SignalBeam.{Service}.Application.Queries;

public record {Name}Query({parameters});
```

For list queries, include pagination:
```csharp
public record List{Entity}Query(Guid TenantId, int PageNumber = 1, int PageSize = 20, string? Search = null);
```

## 2. Response Record (`Application/Contracts/`)

For single entity:
```csharp
public record {Name}Response({fields});
```

For lists, return paged response:
```csharp
public record {Name}ListResponse(IReadOnlyCollection<{Name}Response> Items, int TotalCount, int PageNumber, int PageSize);
```

## 3. Handler (`Application/Queries/{Name}Handler.cs`)

```csharp
namespace SignalBeam.{Service}.Application.Queries;

public class {Name}Handler
{
    private readonly I{Entity}QueryRepository _queryRepository;

    public {Name}Handler(I{Entity}QueryRepository queryRepository)
    {
        _queryRepository = queryRepository;
    }

    public async Task<Result<{Name}Response>> Handle({Name}Query query, CancellationToken cancellationToken)
    {
        // Use query repository (read-optimized, potentially Dapper)
        // Return Result.Success or Result.Failure("NOT_FOUND", ...)
    }
}
```

## 4. Query Repository Method

Add to the query repository interface in Domain:
```csharp
Task<{Entity}?> GetByIdAsync({Entity}Id id, CancellationToken cancellationToken = default);
```

Implement in Infrastructure using EF Core or Dapper for read-optimized queries.

## 5. Endpoint (add to existing `Host/Endpoints/{Domain}Endpoints.cs`)

```csharp
group.MapGet("/{route}", {Name})
    .WithName("{Name}")
    .WithSummary("...")
    .Produces<{Name}Response>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status404NotFound);

private static async Task<IResult> {Name}(
    [FromRoute] Guid id,
    [FromServices] {Name}Handler handler,
    CancellationToken cancellationToken)
{
    var query = new {Name}Query(id);
    var result = await handler.Handle(query, cancellationToken);
    return result.ToHttpResult();
}
```

For list endpoints, use `[FromQuery]` for pagination:
```csharp
[FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null
```

## Checklist
- [ ] Query record is immutable
- [ ] Uses query repository (read side), not command repository
- [ ] Handler returns `Result<T>`
- [ ] Endpoint has OpenAPI metadata
- [ ] Pagination supported for list queries
- [ ] CancellationToken propagated
