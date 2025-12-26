using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Commands;

/// <summary>
/// Command to resume a paused rollout.
/// </summary>
public record ResumeRolloutCommand(
    Guid RolloutId);

/// <summary>
/// Response after resuming a rollout.
/// </summary>
public record ResumeRolloutResponse(
    Guid RolloutId,
    DateTimeOffset ResumedAt,
    int CurrentPhaseNumber,
    string Status);

/// <summary>
/// Handler for ResumeRolloutCommand.
/// </summary>
public class ResumeRolloutHandler
{
    private readonly IRolloutRepository _rolloutRepository;

    public ResumeRolloutHandler(IRolloutRepository rolloutRepository)
    {
        _rolloutRepository = rolloutRepository;
    }

    public async Task<Result<ResumeRolloutResponse>> Handle(
        ResumeRolloutCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load rollout
        var rollout = await _rolloutRepository.GetByIdAsync(command.RolloutId, cancellationToken);
        if (rollout is null)
        {
            return Result.Failure<ResumeRolloutResponse>(
                Error.NotFound("ROLLOUT_NOT_FOUND", $"Rollout {command.RolloutId} not found."));
        }

        // 2. Resume rollout (domain logic validates status)
        try
        {
            var resumedAt = DateTimeOffset.UtcNow;
            rollout.Resume(resumedAt);

            // 3. Save changes
            await _rolloutRepository.UpdateAsync(rollout, cancellationToken);
            await _rolloutRepository.SaveChangesAsync(cancellationToken);

            // 4. Return response
            return Result<ResumeRolloutResponse>.Success(new ResumeRolloutResponse(
                rollout.Id,
                resumedAt,
                rollout.CurrentPhaseNumber,
                rollout.Status.ToString()));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<ResumeRolloutResponse>(
                Error.Validation("INVALID_OPERATION", ex.Message));
        }
    }
}
