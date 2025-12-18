using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Queries;

/// <summary>
/// Query to get rollout status for a bundle.
/// </summary>
public record GetRolloutStatusQuery(string BundleId);

/// <summary>
/// Rollout status DTO.
/// </summary>
public record RolloutStatusDto(
    Guid RolloutId,
    Guid BundleId,
    string BundleVersion,
    Guid DeviceId,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage,
    int RetryCount);

/// <summary>
/// Response for GetRolloutStatusQuery.
/// </summary>
public record GetRolloutStatusResponse(
    IReadOnlyList<RolloutStatusDto> Rollouts,
    int TotalCount,
    int PendingCount,
    int InProgressCount,
    int SucceededCount,
    int FailedCount);

/// <summary>
/// Handler for GetRolloutStatusQuery.
/// Uses Wolverine's IMessageHandler pattern.
/// </summary>
public class GetRolloutStatusHandler
{
    private readonly IRolloutStatusRepository _rolloutStatusRepository;

    public GetRolloutStatusHandler(IRolloutStatusRepository rolloutStatusRepository)
    {
        _rolloutStatusRepository = rolloutStatusRepository;
    }

    public async Task<Result<GetRolloutStatusResponse>> Handle(
        GetRolloutStatusQuery query,
        CancellationToken cancellationToken)
    {
        // Parse and validate bundle ID
        if (!BundleId.TryParse(query.BundleId, out var bundleId))
        {
            return Result.Failure<GetRolloutStatusResponse>(
                Error.Validation("INVALID_BUNDLE_ID", $"Invalid bundle ID format: {query.BundleId}"));
        }

        // Get all rollout statuses for the bundle
        var rollouts = await _rolloutStatusRepository.GetByBundleAsync(bundleId, cancellationToken);

        // Map to DTOs
        var rolloutDtos = rollouts.Select(r => new RolloutStatusDto(
            r.Id,
            r.BundleId.Value,
            r.BundleVersion.ToString(),
            r.DeviceId.Value,
            r.Status.ToString(),
            r.StartedAt,
            r.CompletedAt,
            r.ErrorMessage,
            r.RetryCount
        )).ToList();

        // Calculate status counts
        var totalCount = rollouts.Count;
        var pendingCount = rollouts.Count(r => r.Status == Domain.Entities.RolloutState.Pending);
        var inProgressCount = rollouts.Count(r => r.Status == Domain.Entities.RolloutState.InProgress);
        var succeededCount = rollouts.Count(r => r.Status == Domain.Entities.RolloutState.Succeeded);
        var failedCount = rollouts.Count(r => r.Status == Domain.Entities.RolloutState.Failed);

        return Result<GetRolloutStatusResponse>.Success(new GetRolloutStatusResponse(
            rolloutDtos,
            totalCount,
            pendingCount,
            inProgressCount,
            succeededCount,
            failedCount));
    }
}
