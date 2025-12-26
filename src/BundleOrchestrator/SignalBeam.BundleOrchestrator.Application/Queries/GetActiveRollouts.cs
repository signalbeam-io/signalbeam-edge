using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Queries;

/// <summary>
/// Query to get all active rollouts (InProgress or Paused) for a tenant.
/// Used by background orchestration services to monitor and progress rollouts.
/// </summary>
public record GetActiveRolloutsQuery(Guid TenantId);

/// <summary>
/// Active rollout summary with current phase information.
/// </summary>
public record ActiveRolloutDto(
    Guid RolloutId,
    Guid BundleId,
    string Name,
    string TargetVersion,
    string Status,
    int CurrentPhaseNumber,
    string CurrentPhaseName,
    int CurrentPhaseTargetCount,
    int CurrentPhaseSuccessCount,
    int CurrentPhaseFailureCount,
    decimal CurrentPhaseSuccessRate,
    DateTimeOffset? PhaseStartedAt,
    DateTimeOffset StartedAt,
    decimal FailureThreshold);

/// <summary>
/// Response containing all active rollouts.
/// </summary>
public record GetActiveRolloutsResponse(
    IReadOnlyList<ActiveRolloutDto> ActiveRollouts,
    int TotalCount);

/// <summary>
/// Handler for GetActiveRolloutsQuery.
/// </summary>
public class GetActiveRolloutsHandler
{
    private readonly IRolloutRepository _rolloutRepository;

    public GetActiveRolloutsHandler(IRolloutRepository rolloutRepository)
    {
        _rolloutRepository = rolloutRepository;
    }

    public async Task<Result<GetActiveRolloutsResponse>> Handle(
        GetActiveRolloutsQuery query,
        CancellationToken cancellationToken)
    {
        // 1. Validate tenant ID
        var tenantId = new TenantId(query.TenantId);

        // 2. Get active rollouts (InProgress or Paused)
        var activeRollouts = await _rolloutRepository.GetActiveRolloutsAsync(tenantId, cancellationToken);

        // 3. Map to DTOs
        var rolloutDtos = activeRollouts.Select(rollout =>
        {
            var currentPhase = rollout.GetCurrentPhase();
            if (currentPhase is null)
            {
                throw new InvalidOperationException($"Active rollout {rollout.Id} has no current phase.");
            }

            return new ActiveRolloutDto(
                rollout.Id,
                rollout.BundleId.Value,
                rollout.Name,
                rollout.TargetVersion.ToString(),
                rollout.Status.ToString(),
                rollout.CurrentPhaseNumber,
                currentPhase.Name,
                currentPhase.TargetDeviceCount,
                currentPhase.SuccessCount,
                currentPhase.FailureCount,
                currentPhase.GetSuccessRate(),
                currentPhase.StartedAt,
                rollout.StartedAt!.Value,
                rollout.FailureThreshold);
        }).ToList();

        // 4. Build response
        var response = new GetActiveRolloutsResponse(
            rolloutDtos,
            rolloutDtos.Count);

        return Result<GetActiveRolloutsResponse>.Success(response);
    }
}
