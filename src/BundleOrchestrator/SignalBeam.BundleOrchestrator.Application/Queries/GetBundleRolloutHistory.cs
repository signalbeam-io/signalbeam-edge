using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Queries;

/// <summary>
/// Query to get rollout history for a specific bundle.
/// </summary>
public record GetBundleRolloutHistoryQuery(string BundleId);

/// <summary>
/// Historical rollout entry.
/// </summary>
public record RolloutHistoryEntryDto(
    Guid RolloutId,
    string Name,
    string TargetVersion,
    string? PreviousVersion,
    string Status,
    int TotalPhases,
    int TotalDevices,
    int SucceededDevices,
    int FailedDevices,
    decimal SuccessRate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? CreatedBy);

/// <summary>
/// Response containing rollout history.
/// </summary>
public record GetBundleRolloutHistoryResponse(
    Guid BundleId,
    IReadOnlyList<RolloutHistoryEntryDto> RolloutHistory,
    int TotalCount);

/// <summary>
/// Handler for GetBundleRolloutHistoryQuery.
/// </summary>
public class GetBundleRolloutHistoryHandler
{
    private readonly IRolloutRepository _rolloutRepository;

    public GetBundleRolloutHistoryHandler(IRolloutRepository rolloutRepository)
    {
        _rolloutRepository = rolloutRepository;
    }

    public async Task<Result<GetBundleRolloutHistoryResponse>> Handle(
        GetBundleRolloutHistoryQuery query,
        CancellationToken cancellationToken)
    {
        // 1. Parse and validate bundle ID
        if (!BundleId.TryParse(query.BundleId, out var bundleId))
        {
            return Result.Failure<GetBundleRolloutHistoryResponse>(
                Error.Validation("INVALID_BUNDLE_ID", $"Invalid bundle ID: {query.BundleId}"));
        }

        // 2. Get all rollouts for this bundle
        var rollouts = await _rolloutRepository.GetByBundleIdAsync(bundleId, cancellationToken);

        // 3. Map to history entry DTOs
        var historyEntries = rollouts.Select(rollout =>
        {
            var (total, succeeded, failed, successRate) = rollout.GetOverallProgress();

            return new RolloutHistoryEntryDto(
                rollout.Id,
                rollout.Name,
                rollout.TargetVersion.ToString(),
                rollout.PreviousVersion?.ToString(),
                rollout.Status.ToString(),
                rollout.Phases.Count,
                total,
                succeeded,
                failed,
                successRate,
                rollout.CreatedAt,
                rollout.StartedAt,
                rollout.CompletedAt,
                rollout.CreatedBy);
        }).ToList();

        // 4. Build response
        var response = new GetBundleRolloutHistoryResponse(
            bundleId.Value,
            historyEntries,
            historyEntries.Count);

        return Result<GetBundleRolloutHistoryResponse>.Success(response);
    }
}
