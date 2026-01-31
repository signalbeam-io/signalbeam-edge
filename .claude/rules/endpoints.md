---
paths:
  - "src/**/Endpoints/**/*.cs"
  - "src/**/*Endpoints*.cs"
---

# Endpoint Rules (Minimal API)

## Structure
- Define as a static class with a `MapEndpoints(this WebApplication app)` extension method
- Group routes: `var group = app.MapGroup("/api/devices").WithTags("Devices").WithOpenApi();`
- Handler methods are `private static async Task<IResult>` methods in the same class

## OpenAPI Metadata
- Every endpoint has `.WithName()`, `.WithSummary()`, `.WithDescription()`
- Use `.Produces<T>(StatusCodes.Status200OK)` and `.ProducesProblem(StatusCodes.Status404NotFound)` for response types
- Group with `.WithTags("DomainName")`

## DI in Handlers
- Inject services via parameters with `[FromServices]` attribute
- Inject route/query params with `[FromRoute]`, `[FromQuery]`, `[FromBody]`

## Result-to-HTTP Conversion
- Convert `Result<T>` to HTTP using `.ToHttpResult()` extension method
- This maps ErrorType to status codes automatically:
  - Validation -> 400, NotFound -> 404, Conflict -> 409, Unauthorized -> 401, Forbidden -> 403

## Pattern
```csharp
public static class DeviceEndpoints
{
    public static void MapEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/devices")
            .WithTags("Devices")
            .WithOpenApi();

        group.MapPost("/", RegisterDevice)
            .WithName("RegisterDevice")
            .WithSummary("Register a new device");
    }

    private static async Task<IResult> RegisterDevice(
        [FromBody] RegisterDeviceRequest request,
        [FromServices] RegisterDeviceHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(command, cancellationToken);
        return result.ToHttpResult();
    }
}
```
