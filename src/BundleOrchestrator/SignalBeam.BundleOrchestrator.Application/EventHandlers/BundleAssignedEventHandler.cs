using Microsoft.Extensions.Logging;
using SignalBeam.Domain.Events;

namespace SignalBeam.BundleOrchestrator.Application.EventHandlers;

/// <summary>
/// Handler for BundleAssignedEvent.
/// Performs side effects when a bundle is assigned to a device (e.g., logging, NATS publishing to notify edge agents).
/// </summary>
public class BundleAssignedEventHandler
{
    private readonly ILogger<BundleAssignedEventHandler> _logger;

    public BundleAssignedEventHandler(ILogger<BundleAssignedEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task Handle(BundleAssignedEvent domainEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Bundle assigned: BundleId={BundleId} to DeviceId={DeviceId} at {AssignedAt}",
            domainEvent.BundleId,
            domainEvent.DeviceId,
            domainEvent.AssignedAt);

        // TODO: Publish integration event to NATS to notify edge agents
        // Subject: signalbeam.bundles.assignments.<deviceId>
        // Payload: { bundleId, deviceId, assignedAt }
        // This will trigger the edge agent to pull the desired state and reconcile

        // TODO: Update search index or analytics database

        await Task.CompletedTask;
    }
}
