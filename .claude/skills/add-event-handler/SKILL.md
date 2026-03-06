---
name: add-event-handler
description: Scaffold a WolverineFx event handler for domain events or integration events. Use whenever the user wants to react to domain events, publish to NATS, send notifications, update read models, or add async side effects.
allowed-tools: Read, Write, Edit, Glob, Grep, Bash, mcp__context7__resolve-library-id, mcp__context7__query-docs
user-invocable: true
---

# Add Event Handler

When the user asks to add an event handler, scaffold a WolverineFx handler for a domain or integration event.

## Arguments

- `{EventName}` — Name of the event to handle (e.g., `DeviceRegisteredEvent`)
- `{Service}` — Target microservice (ask if ambiguous)

## 1. Find the Event

Search for the event class:
```bash
grep -rn "class {EventName}" src/
```

If the event doesn't exist, suggest running `/add-entity` first or create it inline.

## 2. Event Handler (`Application/Events/{EventName}Handler.cs`)

```csharp
namespace SignalBeam.{Service}.Application.Events;

public class {EventName}Handler
{
    // Constructor-inject repositories, services, or ILogger<T>

    public async Task Handle({EventName} @event, CancellationToken cancellationToken)
    {
        // React to the event:
        // - Update read models
        // - Send notifications
        // - Publish integration events
        // - Trigger side effects
    }
}
```

For handlers that publish to NATS:

```csharp
public class {EventName}Handler
{
    private readonly INatsPublisher _natsPublisher;
    private readonly ILogger<{EventName}Handler> _logger;

    public {EventName}Handler(INatsPublisher natsPublisher, ILogger<{EventName}Handler> logger)
    {
        _natsPublisher = natsPublisher;
        _logger = logger;
    }

    public async Task Handle({EventName} @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {Event} for {Id}", nameof({EventName}), @event.Id);

        await _natsPublisher.PublishAsync(
            "signalbeam.{domain}.events.{event-type}",
            @event,
            cancellationToken);
    }
}
```

## 3. Integration Event (if cross-service)

If the handler needs to publish an event for other services:

```csharp
namespace SignalBeam.{Service}.Application.Events;

public record {Name}IntegrationEvent(
    Guid Id,
    // relevant fields
    DateTimeOffset OccurredAt);
```

## Checklist

- [ ] Handler class in `Application/Events/` folder
- [ ] Event parameter named `@event` (reserved keyword escaping)
- [ ] CancellationToken propagated
- [ ] Logging at appropriate level
- [ ] No return value (event handlers are fire-and-forget)
- [ ] Idempotent handling (events may be delivered more than once)
- [ ] No exceptions thrown for business logic (log and continue)

## Guidelines

- WolverineFx discovers handlers by convention — no explicit registration needed
- One handler per event per concern (don't mix read model updates with notifications)
- Use `ILogger` for observability, not `Console.WriteLine`
- For NATS subjects, follow the hierarchy in CLAUDE.md

## Related Skills

- `/add-entity` to create the domain entity and events first
- `/add-command` if the handler needs to trigger a command
