using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Queries;

/// <summary>
/// Query to get rollout details by ID.
/// </summary>
public record GetRolloutByIdQuery(string RolloutId);

/// <summary>
/// Response containing rollout details with aggregated progress.
/// </summary>
public record GetRolloutByIdResponse(
    Guid RolloutId,
    Guid BundleId,
    string BundleVersion,
    string Status,
    RolloutProgressDto Progress,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

/// <summary>
/// Rollout progress aggregation.
/// </summary>
public record RolloutProgressDto(
    int Total,
    int Pending,
    int InProgress,
    int Succeeded,
    int Failed,
    int Cancelled);

/// <summary>
/// Handler for GetRolloutByIdQuery.
/// </summary>
public class GetRolloutByIdHandler
{
    private readonly IRolloutStatusRepository _rolloutStatusRepository;

    public GetRolloutByIdHandler(IRolloutStatusRepository rolloutStatusRepository)
    {
        _rolloutStatusRepository = rolloutStatusRepository;
    }

    public async Task<Result<GetRolloutByIdResponse>> Handle(
        GetRolloutByIdQuery query,
        CancellationToken cancellationToken)
    {
        // Parse rollout ID
        if (!Guid.TryParse(query.RolloutId, out var rolloutId))
        {
            return Result.Failure<GetRolloutByIdResponse>(
                Error.Validation("INVALID_ROLLOUT_ID", "Invalid rollout ID format."));
        }

        // Get all rollout statuses for this rollout
        var rolloutStatuses = await _rolloutStatusRepository.GetByRolloutIdAsync(
            rolloutId, cancellationToken);

        if (rolloutStatuses.Count == 0)
        {
            return Result.Failure<GetRolloutByIdResponse>(
                Error.NotFound("ROLLOUT_NOT_FOUND", $"Rollout {query.RolloutId} not found."));
        }

        // Aggregate status counts
        var first = rolloutStatuses.First();
        var total = rolloutStatuses.Count;
        var pending = rolloutStatuses.Count(r => r.Status == RolloutState.Pending);
        var inProgress = rolloutStatuses.Count(r => r.Status == RolloutState.InProgress);
        var succeeded = rolloutStatuses.Count(r => r.Status == RolloutState.Succeeded);
        var failed = rolloutStatuses.Count(r => r.Status == RolloutState.Failed);
        var cancelled = rolloutStatuses.Count(r => r.Status == RolloutState.Cancelled);

        // Determine overall rollout status
        string overallStatus;
        if (succeeded == total)
            overallStatus = "completed";
        else if (cancelled > 0)
            overallStatus = "cancelled";
        else if (failed > 0 && (pending + inProgress) == 0)
            overallStatus = "failed";
        else if (inProgress > 0 || succeeded > 0)
            overallStatus = "in_progress";
        else
            overallStatus = "pending";

        // Determine completion time (when last device completed)
        var completedAt = rolloutStatuses
            .Where(r => r.CompletedAt.HasValue)
            .OrderByDescending(r => r.CompletedAt)
            .Select(r => r.CompletedAt)
            .FirstOrDefault();

        return Result<GetRolloutByIdResponse>.Success(new GetRolloutByIdResponse(
            rolloutId,
            first.BundleId.Value,
            first.BundleVersion.ToString(),
            overallStatus,
            new RolloutProgressDto(total, pending, inProgress, succeeded, failed, cancelled),
            first.StartedAt,
            completedAt));
    }
}
