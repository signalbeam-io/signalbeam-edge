using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Queries;

/// <summary>
/// Query to get paginated list of rollouts.
/// </summary>
public record GetRolloutsQuery(
    string? BundleId = null,
    int Page = 1,
    int PageSize = 10);

/// <summary>
/// Paginated response containing rollouts.
/// </summary>
public record GetRolloutsResponse(
    IReadOnlyList<RolloutSummaryDto> Data,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

/// <summary>
/// Summary of a rollout for list view.
/// </summary>
public record RolloutSummaryDto(
    Guid Id,
    Guid TenantId,
    Guid BundleId,
    string Version,
    string TargetType,
    List<string> TargetIds,
    string Status,
    RolloutProgressDto Progress,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);

/// <summary>
/// Handler for GetRolloutsQuery.
/// </summary>
public class GetRolloutsHandler
{
    private readonly IRolloutStatusRepository _rolloutStatusRepository;

    public GetRolloutsHandler(IRolloutStatusRepository rolloutStatusRepository)
    {
        _rolloutStatusRepository = rolloutStatusRepository;
    }

    public async Task<Result<GetRolloutsResponse>> Handle(
        GetRolloutsQuery query,
        CancellationToken cancellationToken)
    {
        // Parse bundle ID if provided
        BundleId? bundleId = null;
        if (!string.IsNullOrEmpty(query.BundleId))
        {
            if (!Guid.TryParse(query.BundleId, out var bundleGuid))
            {
                return Result.Failure<GetRolloutsResponse>(
                    Error.Validation("INVALID_BUNDLE_ID", "Invalid bundle ID format."));
            }
            bundleId = new BundleId(bundleGuid);
        }

        // Get distinct rollouts with pagination
        var (rolloutIds, totalCount) = await _rolloutStatusRepository.GetDistinctRolloutsAsync(
            bundleId,
            query.Page,
            query.PageSize,
            cancellationToken);

        // Fetch details for each rollout
        var rolloutSummaries = new List<RolloutSummaryDto>();
        foreach (var rolloutId in rolloutIds)
        {
            var rolloutStatuses = await _rolloutStatusRepository.GetByRolloutIdAsync(
                rolloutId, cancellationToken);

            if (rolloutStatuses.Count > 0)
            {
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

                // Determine most recent update time
                var updatedAt = rolloutStatuses
                    .Where(r => r.CompletedAt.HasValue)
                    .OrderByDescending(r => r.CompletedAt)
                    .Select(r => r.CompletedAt!.Value)
                    .FirstOrDefault(first.StartedAt);

                rolloutSummaries.Add(new RolloutSummaryDto(
                    rolloutId,
                    Guid.Empty, // TenantId - TODO: Add tenant support
                    first.BundleId.Value,
                    first.BundleVersion.ToString(),
                    "device", // TODO: Determine target type from metadata
                    rolloutStatuses.Select(r => r.DeviceId.Value.ToString()).ToList(),
                    overallStatus,
                    new RolloutProgressDto(total, pending, inProgress, succeeded, failed, cancelled),
                    first.StartedAt,
                    updatedAt,
                    completedAt));
            }
        }

        var totalPages = (int)Math.Ceiling((double)totalCount / query.PageSize);

        return Result<GetRolloutsResponse>.Success(new GetRolloutsResponse(
            rolloutSummaries,
            query.Page,
            query.PageSize,
            totalCount,
            totalPages));
    }
}
