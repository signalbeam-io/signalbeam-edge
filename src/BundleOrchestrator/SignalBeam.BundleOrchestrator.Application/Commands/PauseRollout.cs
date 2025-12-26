using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Commands;

/// <summary>
/// Command to pause an in-progress rollout.
/// </summary>
public record PauseRolloutCommand(
    Guid RolloutId);

/// <summary>
/// Response after pausing a rollout.
/// </summary>
public record PauseRolloutResponse(
    Guid RolloutId,
    int CurrentPhaseNumber,
    string Status);

/// <summary>
/// Handler for PauseRolloutCommand.
/// </summary>
public class PauseRolloutHandler
{
    private readonly IRolloutRepository _rolloutRepository;

    public PauseRolloutHandler(IRolloutRepository rolloutRepository)
    {
        _rolloutRepository = rolloutRepository;
    }

    public async Task<Result<PauseRolloutResponse>> Handle(
        PauseRolloutCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load rollout
        var rollout = await _rolloutRepository.GetByIdAsync(command.RolloutId, cancellationToken);
        if (rollout is null)
        {
            return Result.Failure<PauseRolloutResponse>(
                Error.NotFound("ROLLOUT_NOT_FOUND", $"Rollout {command.RolloutId} not found."));
        }

        // 2. Pause rollout (domain logic validates status)
        try
        {
            rollout.Pause();

            // 3. Save changes
            await _rolloutRepository.UpdateAsync(rollout, cancellationToken);
            await _rolloutRepository.SaveChangesAsync(cancellationToken);

            // 4. Return response
            return Result<PauseRolloutResponse>.Success(new PauseRolloutResponse(
                rollout.Id,
                rollout.CurrentPhaseNumber,
                rollout.Status.ToString()));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<PauseRolloutResponse>(
                Error.Validation("INVALID_OPERATION", ex.Message));
        }
    }
}
