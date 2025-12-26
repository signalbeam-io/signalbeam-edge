using Microsoft.Extensions.Logging;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Application.Services;

/// <summary>
/// Service responsible for orchestrating automatic phased rollout progression.
/// Monitors active rollouts and advances phases when health conditions are met.
/// </summary>
public class RolloutOrchestrationService
{
    private readonly IRolloutRepository _rolloutRepository;
    private readonly ILogger<RolloutOrchestrationService> _logger;

    public RolloutOrchestrationService(
        IRolloutRepository rolloutRepository,
        ILogger<RolloutOrchestrationService> logger)
    {
        _rolloutRepository = rolloutRepository;
        _logger = logger;
    }

    /// <summary>
    /// Process all active rollouts for a tenant and advance phases when conditions are met.
    /// </summary>
    public async Task ProcessActiveRolloutsAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing active rollouts for tenant {TenantId}", tenantId);

        var activeRollouts = await _rolloutRepository.GetActiveRolloutsAsync(tenantId, cancellationToken);

        foreach (var rollout in activeRollouts)
        {
            try
            {
                await ProcessRolloutAsync(rollout, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing rollout {RolloutId}", rollout.Id);
            }
        }
    }

    /// <summary>
    /// Process a single rollout and determine if phase advancement or rollback is needed.
    /// </summary>
    private async Task ProcessRolloutAsync(
        Rollout rollout,
        CancellationToken cancellationToken)
    {
        // Skip paused rollouts
        if (rollout.Status == RolloutLifecycleStatus.Paused)
        {
            _logger.LogDebug("Rollout {RolloutId} is paused, skipping", rollout.Id);
            return;
        }

        var currentPhase = rollout.GetCurrentPhase();
        if (currentPhase is null)
        {
            _logger.LogWarning("Active rollout {RolloutId} has no current phase", rollout.Id);
            return;
        }

        // Check for failure threshold breach
        if (ShouldTriggerAutomaticRollback(rollout, currentPhase))
        {
            _logger.LogWarning(
                "Rollout {RolloutId} exceeded failure threshold, triggering automatic rollback",
                rollout.Id);

            rollout.Rollback(DateTimeOffset.UtcNow);
            await _rolloutRepository.UpdateAsync(rollout, cancellationToken);

            // Note: Device state updates would be handled by a separate service
            // that subscribes to RolloutRolledBackEvent
            return;
        }

        // Check if current phase is ready to advance
        if (ShouldAdvancePhase(rollout, currentPhase))
        {
            _logger.LogInformation(
                "Rollout {RolloutId} phase {PhaseNumber} conditions met, advancing to next phase",
                rollout.Id,
                rollout.CurrentPhaseNumber);

            await AdvanceToNextPhaseAsync(rollout, cancellationToken);
        }
    }

    /// <summary>
    /// Determine if the rollout has exceeded its failure threshold and should be rolled back.
    /// </summary>
    private bool ShouldTriggerAutomaticRollback(
        Rollout rollout,
        RolloutPhase currentPhase)
    {
        // Get failure rate for current phase
        var failureRate = currentPhase.GetFailureRate();

        if (failureRate > rollout.FailureThreshold)
        {
            _logger.LogWarning(
                "Rollout {RolloutId} phase {PhaseNumber} failure rate {FailureRate:P2} exceeds threshold {Threshold:P2}",
                rollout.Id,
                rollout.CurrentPhaseNumber,
                failureRate,
                rollout.FailureThreshold);

            return true;
        }

        return false;
    }

    /// <summary>
    /// Determine if the current phase has met all conditions to advance to the next phase.
    /// </summary>
    private bool ShouldAdvancePhase(
        Rollout rollout,
        RolloutPhase currentPhase)
    {
        // Phase must be in progress
        if (currentPhase.Status != PhaseStatus.InProgress)
        {
            return false;
        }

        // All devices in phase must have completed reconciliation
        var hasUnreconciledDevices = currentPhase.DeviceAssignments
            .Any(a => a.Status is DeviceAssignmentStatus.Pending or DeviceAssignmentStatus.Assigned or DeviceAssignmentStatus.Reconciling);

        if (hasUnreconciledDevices)
        {
            _logger.LogDebug(
                "Rollout {RolloutId} phase {PhaseNumber} waiting for device reconciliation",
                rollout.Id,
                rollout.CurrentPhaseNumber);
            return false;
        }

        // Check health requirements
        if (!currentPhase.IsHealthy(rollout.FailureThreshold))
        {
            _logger.LogDebug(
                "Rollout {RolloutId} phase {PhaseNumber} health check failed",
                rollout.Id,
                rollout.CurrentPhaseNumber);
            return false;
        }

        // Check minimum healthy duration if configured
        if (currentPhase.MinHealthyDuration.HasValue && currentPhase.StartedAt.HasValue)
        {
            var healthyDuration = DateTimeOffset.UtcNow - currentPhase.StartedAt.Value;
            if (healthyDuration < currentPhase.MinHealthyDuration.Value)
            {
                _logger.LogDebug(
                    "Rollout {RolloutId} phase {PhaseNumber} waiting for minimum healthy duration ({Current} < {Required})",
                    rollout.Id,
                    rollout.CurrentPhaseNumber,
                    healthyDuration,
                    currentPhase.MinHealthyDuration.Value);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Advance the rollout to the next phase.
    /// </summary>
    private async Task AdvanceToNextPhaseAsync(
        Rollout rollout,
        CancellationToken cancellationToken)
    {
        // Complete current phase
        rollout.CompleteCurrentPhase(DateTimeOffset.UtcNow);

        // Check if this was the last phase
        if (rollout.CurrentPhaseNumber > rollout.Phases.Count)
        {
            _logger.LogInformation("Rollout {RolloutId} completed all phases", rollout.Id);
            await _rolloutRepository.UpdateAsync(rollout, cancellationToken);
            return;
        }

        // Advance to next phase
        rollout.AdvancePhase();

        await _rolloutRepository.UpdateAsync(rollout, cancellationToken);

        _logger.LogInformation(
            "Rollout {RolloutId} advanced to phase {PhaseNumber}",
            rollout.Id,
            rollout.CurrentPhaseNumber);

        // Note: Device selection and assignment for the next phase would be handled by
        // a domain event handler that subscribes to RolloutPhaseAdvancedEvent
        // This allows for better separation of concerns and testability
    }
}
