using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Commands;

/// <summary>
/// Command to update the status of a rollout.
/// </summary>
public record UpdateRolloutStatusCommand(
    Guid RolloutId,
    string Status,
    string? ErrorMessage = null);

/// <summary>
/// Response after updating rollout status.
/// </summary>
public record UpdateRolloutStatusResponse(
    Guid RolloutId,
    string Status,
    DateTimeOffset? CompletedAt);

/// <summary>
/// Handler for UpdateRolloutStatusCommand.
/// Uses Wolverine's IMessageHandler pattern.
/// </summary>
public class UpdateRolloutStatusHandler
{
    private readonly IRolloutStatusRepository _rolloutStatusRepository;

    public UpdateRolloutStatusHandler(IRolloutStatusRepository rolloutStatusRepository)
    {
        _rolloutStatusRepository = rolloutStatusRepository;
    }

    public async Task<Result<UpdateRolloutStatusResponse>> Handle(
        UpdateRolloutStatusCommand command,
        CancellationToken cancellationToken)
    {
        // Get rollout status
        var rolloutStatus = await _rolloutStatusRepository.GetByIdAsync(command.RolloutId, cancellationToken);
        if (rolloutStatus is null)
        {
            return Result.Failure<UpdateRolloutStatusResponse>(
                Error.NotFound("ROLLOUT_NOT_FOUND", $"Rollout with ID {command.RolloutId} not found."));
        }

        // Parse status
        if (!Enum.TryParse<RolloutState>(command.Status, true, out var newStatus))
        {
            return Result.Failure<UpdateRolloutStatusResponse>(
                Error.Validation(
                    "INVALID_STATUS",
                    $"Invalid rollout status: {command.Status}. Valid values are: Pending, InProgress, Succeeded, Failed."));
        }

        var now = DateTimeOffset.UtcNow;

        // Update status based on new state
        try
        {
            switch (newStatus)
            {
                case RolloutState.InProgress:
                    rolloutStatus.MarkInProgress();
                    break;

                case RolloutState.Succeeded:
                    rolloutStatus.MarkSucceeded(now);
                    break;

                case RolloutState.Failed:
                    if (string.IsNullOrWhiteSpace(command.ErrorMessage))
                    {
                        return Result.Failure<UpdateRolloutStatusResponse>(
                            Error.Validation("ERROR_MESSAGE_REQUIRED", "Error message is required when marking rollout as failed."));
                    }
                    rolloutStatus.MarkFailed(command.ErrorMessage, now);
                    break;

                case RolloutState.Pending:
                    // Retry - increment retry count
                    rolloutStatus.IncrementRetryCount();
                    break;

                default:
                    return Result.Failure<UpdateRolloutStatusResponse>(
                        Error.Validation("UNSUPPORTED_STATUS", $"Status {newStatus} is not supported for updates."));
            }

            // Save changes
            await _rolloutStatusRepository.UpdateAsync(rolloutStatus, cancellationToken);
            await _rolloutStatusRepository.SaveChangesAsync(cancellationToken);

            // Return response
            return Result<UpdateRolloutStatusResponse>.Success(new UpdateRolloutStatusResponse(
                rolloutStatus.Id,
                rolloutStatus.Status.ToString(),
                rolloutStatus.CompletedAt));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<UpdateRolloutStatusResponse>(
                Error.Validation("INVALID_STATUS_TRANSITION", ex.Message));
        }
    }
}
