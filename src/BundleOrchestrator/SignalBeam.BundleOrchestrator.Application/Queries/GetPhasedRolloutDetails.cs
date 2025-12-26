using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Enums;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Queries;

/// <summary>
/// Query to get detailed information about a phased rollout including all phases and device assignments.
/// </summary>
public record GetPhasedRolloutDetailsQuery(Guid RolloutId);

/// <summary>
/// Device assignment details within a phase.
/// </summary>
public record DeviceAssignmentDto(
    Guid DeviceId,
    string Status,
    DateTimeOffset? AssignedAt,
    DateTimeOffset? ReconciledAt,
    string? ErrorMessage,
    int RetryCount);

/// <summary>
/// Phase details DTO.
/// </summary>
public record RolloutPhaseDto(
    Guid PhaseId,
    int PhaseNumber,
    string Name,
    int TargetDeviceCount,
    decimal? TargetPercentage,
    string Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    int SuccessCount,
    int FailureCount,
    decimal SuccessRate,
    decimal FailureRate,
    int? MinHealthyDurationMinutes,
    IReadOnlyList<DeviceAssignmentDto> DeviceAssignments);

/// <summary>
/// Overall rollout progress summary.
/// </summary>
public record RolloutProgressSummaryDto(
    int TotalDevicesAcrossPhases,
    int SucceededDevices,
    int FailedDevices,
    decimal OverallSuccessRate,
    int CompletedPhases,
    int TotalPhases);

/// <summary>
/// Complete rollout details response.
/// </summary>
public record GetPhasedRolloutDetailsResponse(
    Guid RolloutId,
    Guid TenantId,
    Guid BundleId,
    string TargetVersion,
    string? PreviousVersion,
    string Name,
    string? Description,
    string Status,
    Guid? TargetDeviceGroupId,
    string? CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    decimal FailureThreshold,
    int CurrentPhaseNumber,
    RolloutProgressSummaryDto Progress,
    IReadOnlyList<RolloutPhaseDto> Phases);

/// <summary>
/// Handler for GetPhasedRolloutDetailsQuery.
/// </summary>
public class GetPhasedRolloutDetailsHandler
{
    private readonly IRolloutRepository _rolloutRepository;

    public GetPhasedRolloutDetailsHandler(IRolloutRepository rolloutRepository)
    {
        _rolloutRepository = rolloutRepository;
    }

    public async Task<Result<GetPhasedRolloutDetailsResponse>> Handle(
        GetPhasedRolloutDetailsQuery query,
        CancellationToken cancellationToken)
    {
        // 1. Load rollout with all related data
        var rollout = await _rolloutRepository.GetByIdAsync(query.RolloutId, cancellationToken);
        if (rollout is null)
        {
            return Result.Failure<GetPhasedRolloutDetailsResponse>(
                Error.NotFound("ROLLOUT_NOT_FOUND", $"Rollout {query.RolloutId} not found."));
        }

        // 2. Map phases to DTOs
        var phaseDtos = rollout.Phases.Select(phase =>
        {
            var assignmentDtos = phase.DeviceAssignments.Select(assignment =>
                new DeviceAssignmentDto(
                    assignment.DeviceId.Value,
                    assignment.Status.ToString(),
                    assignment.AssignedAt,
                    assignment.ReconciledAt,
                    assignment.ErrorMessage,
                    assignment.RetryCount)).ToList();

            var minHealthyDurationMinutes = phase.MinHealthyDuration.HasValue
                ? (int)phase.MinHealthyDuration.Value.TotalMinutes
                : (int?)null;

            return new RolloutPhaseDto(
                phase.Id,
                phase.PhaseNumber,
                phase.Name,
                phase.TargetDeviceCount,
                phase.TargetPercentage,
                phase.Status.ToString(),
                phase.StartedAt,
                phase.CompletedAt,
                phase.SuccessCount,
                phase.FailureCount,
                phase.GetSuccessRate(),
                phase.GetFailureRate(),
                minHealthyDurationMinutes,
                assignmentDtos);
        }).ToList();

        // 3. Calculate overall progress
        var (total, succeeded, failed, successRate) = rollout.GetOverallProgress();
        var completedPhases = rollout.Phases.Count(p => p.Status == PhaseStatus.Completed);

        var progress = new RolloutProgressSummaryDto(
            total,
            succeeded,
            failed,
            successRate,
            completedPhases,
            rollout.Phases.Count);

        // 4. Build response
        var response = new GetPhasedRolloutDetailsResponse(
            rollout.Id,
            rollout.TenantId.Value,
            rollout.BundleId.Value,
            rollout.TargetVersion.ToString(),
            rollout.PreviousVersion?.ToString(),
            rollout.Name,
            rollout.Description,
            rollout.Status.ToString(),
            rollout.TargetDeviceGroupId,
            rollout.CreatedBy,
            rollout.CreatedAt,
            rollout.StartedAt,
            rollout.CompletedAt,
            rollout.FailureThreshold,
            rollout.CurrentPhaseNumber,
            progress,
            phaseDtos);

        return Result<GetPhasedRolloutDetailsResponse>.Success(response);
    }
}
