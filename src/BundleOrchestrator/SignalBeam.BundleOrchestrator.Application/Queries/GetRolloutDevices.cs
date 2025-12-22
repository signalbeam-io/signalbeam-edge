using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Queries;

/// <summary>
/// Query to get device-level rollout status.
/// </summary>
public record GetRolloutDevicesQuery(string RolloutId);

/// <summary>
/// Device-level rollout status DTO.
/// </summary>
public record DeviceRolloutStatusDto(
    Guid DeviceId,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage,
    int RetryCount);

/// <summary>
/// Handler for GetRolloutDevicesQuery.
/// </summary>
public class GetRolloutDevicesHandler
{
    private readonly IRolloutStatusRepository _rolloutStatusRepository;

    public GetRolloutDevicesHandler(IRolloutStatusRepository rolloutStatusRepository)
    {
        _rolloutStatusRepository = rolloutStatusRepository;
    }

    public async Task<Result<List<DeviceRolloutStatusDto>>> Handle(
        GetRolloutDevicesQuery query,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(query.RolloutId, out var rolloutId))
        {
            return Result.Failure<List<DeviceRolloutStatusDto>>(
                Error.Validation("INVALID_ROLLOUT_ID", "Invalid rollout ID format."));
        }

        var rolloutStatuses = await _rolloutStatusRepository.GetByRolloutIdAsync(
            rolloutId, cancellationToken);

        if (rolloutStatuses.Count == 0)
        {
            return Result.Failure<List<DeviceRolloutStatusDto>>(
                Error.NotFound("ROLLOUT_NOT_FOUND", $"Rollout {query.RolloutId} not found."));
        }

        var deviceStatuses = rolloutStatuses.Select(r => new DeviceRolloutStatusDto(
            r.DeviceId.Value,
            r.Status.ToString().ToLowerInvariant(),
            r.StartedAt,
            r.CompletedAt,
            r.ErrorMessage,
            r.RetryCount
        )).ToList();

        return Result<List<DeviceRolloutStatusDto>>.Success(deviceStatuses);
    }
}
