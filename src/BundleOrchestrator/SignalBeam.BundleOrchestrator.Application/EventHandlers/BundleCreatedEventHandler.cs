using Microsoft.Extensions.Logging;
using SignalBeam.Domain.Events;

namespace SignalBeam.BundleOrchestrator.Application.EventHandlers;

/// <summary>
/// Handler for BundleCreatedEvent.
/// Performs side effects when a bundle is created (e.g., logging, notifications, NATS publishing).
/// </summary>
public class BundleCreatedEventHandler
{
    private readonly ILogger<BundleCreatedEventHandler> _logger;

    public BundleCreatedEventHandler(ILogger<BundleCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task Handle(BundleCreatedEvent domainEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Bundle created: {BundleId} for tenant {TenantId} with name {BundleName}",
            domainEvent.BundleId,
            domainEvent.TenantId,
            domainEvent.Name);

        // TODO: Publish integration event to NATS for other services
        // Subject: signalbeam.bundles.created
        // Payload: { bundleId, tenantId, name, createdAt }

        // TODO: Send notification/webhook to external systems

        await Task.CompletedTask;
    }
}
