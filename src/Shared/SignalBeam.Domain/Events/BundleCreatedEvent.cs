using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Raised when a new bundle is created.
/// </summary>
public record BundleCreatedEvent(
    BundleId BundleId,
    TenantId TenantId,
    string Name,
    DateTimeOffset CreatedAt) : DomainEvent;
