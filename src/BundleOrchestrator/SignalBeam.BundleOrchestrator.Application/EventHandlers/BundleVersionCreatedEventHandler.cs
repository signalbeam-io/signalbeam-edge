using Microsoft.Extensions.Logging;
using SignalBeam.Domain.Events;

namespace SignalBeam.BundleOrchestrator.Application.EventHandlers;

/// <summary>
/// Handler for BundleVersionCreatedEvent.
/// Performs side effects when a bundle version is created.
/// </summary>
public class BundleVersionCreatedEventHandler
{
    private readonly ILogger<BundleVersionCreatedEventHandler> _logger;

    public BundleVersionCreatedEventHandler(ILogger<BundleVersionCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task Handle(BundleVersionCreatedEvent domainEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Bundle version created: {VersionId} for bundle {BundleId}, version {Version} with {ContainerCount} containers",
            domainEvent.VersionId,
            domainEvent.BundleId,
            domainEvent.Version,
            domainEvent.ContainerCount);

        // TODO: Publish integration event to NATS for other services
        // Subject: signalbeam.bundles.version.created
        // Payload: { versionId, bundleId, version, containerCount, createdAt }

        await Task.CompletedTask;
    }
}
