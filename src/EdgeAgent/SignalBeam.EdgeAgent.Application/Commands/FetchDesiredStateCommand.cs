using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.EdgeAgent.Application.Commands;

public record FetchDesiredStateCommand(Guid DeviceId);

public class FetchDesiredStateCommandHandler
{
    private readonly ICloudClient _cloudClient;

    public FetchDesiredStateCommandHandler(ICloudClient cloudClient)
    {
        _cloudClient = cloudClient;
    }

    public async Task<Result<DesiredState?>> Handle(
        FetchDesiredStateCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var desiredState = await _cloudClient.FetchDesiredStateAsync(
                command.DeviceId,
                cancellationToken);

            return Result<DesiredState?>.Success(desiredState);
        }
        catch (Exception ex)
        {
            return Result.Failure<DesiredState?>(
                Error.Failure("DesiredState.FetchFailed", $"Failed to fetch desired state: {ex.Message}"));
        }
    }
}
