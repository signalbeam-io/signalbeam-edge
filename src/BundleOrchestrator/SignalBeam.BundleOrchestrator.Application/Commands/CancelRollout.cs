using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Commands;

/// <summary>
/// Command to cancel a rollout.
/// </summary>
public record CancelRolloutCommand(string RolloutId);

/// <summary>
/// Handler for CancelRolloutCommand.
/// </summary>
public class CancelRolloutHandler
{
    private readonly IRolloutStatusRepository _rolloutStatusRepository;

    public CancelRolloutHandler(IRolloutStatusRepository rolloutStatusRepository)
    {
        _rolloutStatusRepository = rolloutStatusRepository;
    }

    public async Task<Result<Unit>> Handle(
        CancelRolloutCommand command,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(command.RolloutId, out var rolloutId))
        {
            return Result.Failure<Unit>(
                Error.Validation("INVALID_ROLLOUT_ID", "Invalid rollout ID format."));
        }

        var rolloutStatuses = await _rolloutStatusRepository.GetByRolloutIdAsync(
            rolloutId, cancellationToken);

        if (rolloutStatuses.Count == 0)
        {
            return Result.Failure<Unit>(
                Error.NotFound("ROLLOUT_NOT_FOUND", $"Rollout {command.RolloutId} not found."));
        }

        var cancelledAt = DateTimeOffset.UtcNow;

        // Cancel all pending and in-progress rollouts
        foreach (var status in rolloutStatuses)
        {
            if (status.Status == RolloutState.Pending || status.Status == RolloutState.InProgress)
            {
                status.MarkCancelled(cancelledAt);
                await _rolloutStatusRepository.UpdateAsync(status, cancellationToken);
            }
        }

        await _rolloutStatusRepository.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}

/// <summary>
/// Unit type for commands with no return value.
/// </summary>
public record struct Unit
{
    public static Unit Value => default;
}
