using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Queries;

/// <summary>
/// Query to list rollouts with pagination and filtering.
/// </summary>
public record ListPhasedRolloutsQuery(
    Guid TenantId,
    string? Status = null,
    string? BundleId = null,
    int Page = 1,
    int PageSize = 10);

/// <summary>
/// Lightweight phased rollout summary for list view.
/// </summary>
public record PhasedRolloutSummaryDto(
    Guid RolloutId,
    Guid BundleId,
    string Name,
    string TargetVersion,
    string Status,
    int CurrentPhaseNumber,
    int TotalPhases,
    int TotalDevices,
    int SucceededDevices,
    int FailedDevices,
    decimal SuccessRate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);

/// <summary>
/// Paginated list of phased rollouts.
/// </summary>
public record ListPhasedRolloutsResponse(
    IReadOnlyList<PhasedRolloutSummaryDto> Rollouts,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

/// <summary>
/// Handler for ListPhasedRolloutsQuery.
/// </summary>
public class ListPhasedRolloutsHandler
{
    private readonly IRolloutRepository _rolloutRepository;

    public ListPhasedRolloutsHandler(IRolloutRepository rolloutRepository)
    {
        _rolloutRepository = rolloutRepository;
    }

    public async Task<Result<ListPhasedRolloutsResponse>> Handle(
        ListPhasedRolloutsQuery query,
        CancellationToken cancellationToken)
    {
        // 1. Validate tenant ID
        var tenantId = new TenantId(query.TenantId);

        // 2. Parse status filter if provided
        RolloutLifecycleStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<RolloutLifecycleStatus>(query.Status, true, out var parsedStatus))
            {
                return Result.Failure<ListPhasedRolloutsResponse>(
                    Error.Validation("INVALID_STATUS", $"Invalid status filter: {query.Status}. Valid values are: Pending, InProgress, Paused, Completed, Failed, RolledBack, Cancelled"));
            }
            statusFilter = parsedStatus;
        }

        // 3. Validate page and page size
        if (query.Page < 1)
        {
            return Result.Failure<ListPhasedRolloutsResponse>(
                Error.Validation("INVALID_PAGE", "Page must be greater than 0."));
        }

        if (query.PageSize < 1 || query.PageSize > 100)
        {
            return Result.Failure<ListPhasedRolloutsResponse>(
                Error.Validation("INVALID_PAGE_SIZE", "Page size must be between 1 and 100."));
        }

        // 4. Get paginated rollouts
        var (rollouts, totalCount) = await _rolloutRepository.GetPagedAsync(
            tenantId,
            statusFilter,
            query.Page,
            query.PageSize,
            cancellationToken);

        // 5. If bundle filter is provided, filter in memory (or enhance repository)
        if (!string.IsNullOrWhiteSpace(query.BundleId))
        {
            if (!BundleId.TryParse(query.BundleId, out var bundleId))
            {
                return Result.Failure<ListPhasedRolloutsResponse>(
                    Error.Validation("INVALID_BUNDLE_ID", $"Invalid bundle ID: {query.BundleId}"));
            }

            rollouts = rollouts.Where(r => r.BundleId == bundleId).ToList();
            totalCount = rollouts.Count;
        }

        // 6. Map to summary DTOs
        var summaryDtos = rollouts.Select(rollout =>
        {
            var (total, succeeded, failed, successRate) = rollout.GetOverallProgress();

            return new PhasedRolloutSummaryDto(
                rollout.Id,
                rollout.BundleId.Value,
                rollout.Name,
                rollout.TargetVersion.ToString(),
                rollout.Status.ToString(),
                rollout.CurrentPhaseNumber,
                rollout.Phases.Count,
                total,
                succeeded,
                failed,
                successRate,
                rollout.CreatedAt,
                rollout.StartedAt,
                rollout.CompletedAt);
        }).ToList();

        // 7. Calculate total pages
        var totalPages = (int)Math.Ceiling((double)totalCount / query.PageSize);

        // 8. Build response
        var response = new ListPhasedRolloutsResponse(
            summaryDtos,
            query.Page,
            query.PageSize,
            totalCount,
            totalPages);

        return Result<ListPhasedRolloutsResponse>.Success(response);
    }
}
