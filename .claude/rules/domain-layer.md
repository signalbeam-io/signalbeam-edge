---
paths:
  - "src/**/Domain/**/*.cs"
  - "src/SignalBeam.Domain/**/*.cs"
---

# Domain Layer Rules

## Zero External Dependencies
- No references to Infrastructure, Host, EF Core, ASP.NET, or any framework
- Only pure C# and domain abstractions

## Entities
- Inherit from `Entity<TId>` where `TId` is a strongly-typed value object
- Identity equality based on ID only
- Protected parameterless constructor for EF Core deserialization
- Public constructor taking `TId id` that calls `base(id)`

## Aggregate Roots
- Inherit from `AggregateRoot<TId>` which extends `Entity<TId>`
- Use static factory methods for creation: `public static Device Register(TenantId tenantId, string name, ...)`
- Raise domain events inside factory methods and state-changing methods: `RaiseDomainEvent(new DeviceRegisteredEvent(...))`
- Only aggregate roots are directly persisted via repositories

## Value Objects
- Inherit from abstract `ValueObject`
- Implement `GetEqualityComponents()` returning `IEnumerable<object?>`
- Immutable: use `init` properties or readonly fields
- Examples: `DeviceId`, `TenantId`, `BundleVersion`, `ContainerSpec`

## Domain Events
- Inherit from `DomainEvent` abstract base class
- Named in past tense: `DeviceRegisteredEvent`, `BundleAssignedEvent`
- Raised via `RaiseDomainEvent()` inside aggregate root methods
- Contain only the data needed by consumers (IDs, timestamps, key values)

## Repository Interfaces
- Defined in Domain as ports: `IDeviceRepository`, `IBundleRepository`
- Separate command repos (mutations) from query repos (reads) for CQRS
- Async signatures with CancellationToken
