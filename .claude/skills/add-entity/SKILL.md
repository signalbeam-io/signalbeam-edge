---
name: add-entity
description: Scaffold a new domain entity with value object ID, aggregate root, factory method, and domain event. Use whenever the user wants to add a new domain concept, database table, persisted object, or aggregate root to the system.
allowed-tools: Read, Write, Edit, Glob, Grep, Bash, mcp__context7__resolve-library-id, mcp__context7__query-docs
user-invocable: true
---

# Add Domain Entity

When the user asks to add a new entity, scaffold following the project's domain-driven design patterns.

If the entity involves non-trivial EF Core configuration (owned types, complex value conversions, table splitting), use context7 to look up the current EF Core docs for the specific pattern before writing the configuration.

## Arguments

- `{Entity}` — Entity name in PascalCase (e.g., `DeviceGroup`, `RolloutPolicy`)
- `{Service}` — Target microservice that owns this entity (e.g., `DeviceManager`, `BundleOrchestrator`). Ask if ambiguous.

## Prerequisites

- The target microservice must exist under `src/` with Infrastructure and Host projects
- The shared Domain project must exist at `src/Shared/SignalBeam.Domain/`
- EF Core tools must be installed (`dotnet tool list -g | grep ef`)

## 1. Value Object ID (`Domain/ValueObjects/{Entity}Id.cs`)

```csharp
namespace SignalBeam.Domain.ValueObjects;

public class {Entity}Id : ValueObject
{
    public Guid Value { get; init; }

    public {Entity}Id(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("{Entity}Id cannot be empty.", nameof(value));
        Value = value;
    }

    public static {Entity}Id New() => new(Guid.NewGuid());

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

## 2. Entity / Aggregate Root (`Domain/Entities/{Entity}.cs`)

```csharp
namespace SignalBeam.Domain.Entities;

public class {Entity} : AggregateRoot<{Entity}Id>
{
    public TenantId TenantId { get; private set; }
    // Add properties with private setters

    // EF Core constructor
    protected {Entity}() : base(default!) { }

    // Private constructor called by factory method
    private {Entity}({Entity}Id id, TenantId tenantId, ...) : base(id)
    {
        TenantId = tenantId;
        // Set properties
    }

    // Factory method - the ONLY way to create instances
    public static {Entity} Create(TenantId tenantId, ...)
    {
        var id = {Entity}Id.New();
        var entity = new {Entity}(id, tenantId, ...);
        entity.RaiseDomainEvent(new {Entity}CreatedEvent(id, tenantId, DateTimeOffset.UtcNow));
        return entity;
    }

    // State-changing methods raise domain events
    public void Update(...)
    {
        // Modify state
        RaiseDomainEvent(new {Entity}UpdatedEvent(Id, ...));
    }
}
```

## 3. Domain Event (`Domain/Events/{Entity}CreatedEvent.cs`)

```csharp
namespace SignalBeam.Domain.Events;

public class {Entity}CreatedEvent : DomainEvent
{
    public {Entity}Id {Entity}Id { get; }
    public TenantId TenantId { get; }
    public DateTimeOffset OccurredAt { get; }

    public {Entity}CreatedEvent({Entity}Id entityId, TenantId tenantId, DateTimeOffset occurredAt)
    {
        {Entity}Id = entityId;
        TenantId = tenantId;
        OccurredAt = occurredAt;
    }
}
```

## 4. Repository Interface (`Domain/` or `Application/`)

```csharp
public interface I{Entity}Repository
{
    Task<{Entity}?> GetByIdAsync({Entity}Id id, CancellationToken cancellationToken = default);
    Task AddAsync({Entity} entity, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface I{Entity}QueryRepository
{
    Task<{Entity}?> GetByIdAsync({Entity}Id id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyCollection<{Entity}> Items, int TotalCount)> ListAsync(TenantId tenantId, int page, int pageSize, CancellationToken cancellationToken = default);
}
```

## 5. EF Core Configuration (`Infrastructure/Persistence/Configurations/{Entity}Configuration.cs`)

```csharp
public class {Entity}Configuration : IEntityTypeConfiguration<{Entity}>
{
    public void Configure(EntityTypeBuilder<{Entity}> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new {Entity}Id(value));
        builder.Property(x => x.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));
        // Configure other properties
    }
}
```

## 6. Add DbSet to DbContext

```csharp
public DbSet<{Entity}> {Entity}s => Set<{Entity}>();
```

## 7. EF Core Migration (MANDATORY)

After adding the DbSet and entity configuration, **always** create a migration immediately:

```bash
dotnet ef migrations add Add{Entity} \
  --project src/{Service}/{Service}.Infrastructure \
  --startup-project src/{Service}/{Service}.Host
```

Review the generated migration to verify only expected changes are included. This step is **not optional** — skipping it causes `PendingModelChangesWarning` errors at startup.

## Checklist
- [ ] Value object ID with validation and `GetEqualityComponents()`
- [ ] Entity inherits `AggregateRoot<TId>` with protected parameterless constructor
- [ ] Factory method is the only creation path
- [ ] Domain events raised in factory and state-change methods
- [ ] Past-tense event names: `{Entity}CreatedEvent`, `{Entity}UpdatedEvent`
- [ ] Separate command and query repository interfaces
- [ ] EF Core configuration with value object conversions
- [ ] DbSet added to DbContext
- [ ] **EF Core migration created** for the new table
- [ ] No framework dependencies in Domain layer

## Output

After scaffolding, report:
```
## Scaffolded: {Entity}

Files created/modified:
- `src/Shared/SignalBeam.Domain/ValueObjects/{Entity}Id.cs`
- `src/Shared/SignalBeam.Domain/Entities/{Entity}.cs`
- `src/Shared/SignalBeam.Domain/Events/{Entity}CreatedEvent.cs`
- `src/{Service}/{Service}.Application/` — Repository interfaces
- `src/{Service}/{Service}.Infrastructure/Persistence/Configurations/{Entity}Configuration.cs`
- `src/{Service}/{Service}.Infrastructure/Persistence/{Service}DbContext.cs` (modified)
- Migration: `src/{Service}/{Service}.Infrastructure/Persistence/Migrations/{timestamp}_Add{Entity}.cs`

Next: Run `/add-command` or `/add-query` to create endpoints for this entity.
```

## Error Handling

- **DbContext not found:** Search for `*DbContext.cs` in the Infrastructure project. If none exists, the service may not have persistence set up yet — warn the user.
- **EF Core tools not installed:** Run `dotnet tool install --global dotnet-ef` and retry.
- **Migration fails:** Check that the entity configuration is correct and the DbSet was added. Read the error output for hints.
- **Value object ID already exists:** The entity name may conflict with an existing one. Check `src/Shared/SignalBeam.Domain/ValueObjects/` before creating.

## Related Skills

- `/add-command` to create commands that operate on this entity
- `/add-query` to create queries that read this entity
- `/add-event-handler` to handle the domain events raised by this entity
- `/add-migration` to create the EF Core migration for the new table
