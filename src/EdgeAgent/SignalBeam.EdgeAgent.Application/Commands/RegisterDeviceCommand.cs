using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.EdgeAgent.Application.Commands;

public record RegisterDeviceCommand(
    Guid TenantId,
    string DeviceId,
    string RegistrationToken,
    string? Hostname = null,
    string? Platform = null);

public class RegisterDeviceCommandHandler
{
    private readonly ICloudClient _cloudClient;

    public RegisterDeviceCommandHandler(ICloudClient cloudClient)
    {
        _cloudClient = cloudClient;
    }

    public async Task<Result<DeviceRegistrationResponse>> Handle(
        RegisterDeviceCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new DeviceRegistrationRequest(
                command.TenantId,
                command.DeviceId,
                command.RegistrationToken,
                command.Hostname,
                command.Platform);

            var response = await _cloudClient.RegisterDeviceAsync(request, cancellationToken);

            return Result<DeviceRegistrationResponse>.Success(response);
        }
        catch (Exception ex)
        {
            return Result.Failure<DeviceRegistrationResponse>(
                Error.Failure("DeviceRegistration.Failed", $"Failed to register device: {ex.Message}"));
        }
    }
}
