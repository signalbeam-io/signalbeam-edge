using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

/// <summary>
/// Command to reject a pending device registration.
/// </summary>
public record RejectDeviceRegistrationCommand(Guid DeviceId, string? Reason = null);

/// <summary>
/// Response after rejecting device registration.
/// </summary>
public record RejectDeviceRegistrationResponse(Guid DeviceId, string Status, string? Reason);

/// <summary>
/// Handler for RejectDeviceRegistrationCommand.
/// </summary>
public class RejectDeviceRegistrationHandler
{
    private readonly IDeviceRepository _deviceRepository;

    public RejectDeviceRegistrationHandler(IDeviceRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<Result<RejectDeviceRegistrationResponse>> Handle(
        RejectDeviceRegistrationCommand command,
        CancellationToken cancellationToken)
    {
        // Get the device
        var deviceId = new DeviceId(command.DeviceId);
        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);

        if (device == null)
        {
            var error = Error.NotFound(
                "DEVICE_NOT_FOUND",
                $"Device with ID {command.DeviceId} not found.");
            return Result.Failure<RejectDeviceRegistrationResponse>(error);
        }

        // Reject the registration
        try
        {
            device.RejectRegistration(DateTimeOffset.UtcNow, command.Reason);
        }
        catch (InvalidOperationException ex)
        {
            var error = Error.Validation("REJECTION_FAILED", ex.Message);
            return Result.Failure<RejectDeviceRegistrationResponse>(error);
        }

        // Save changes
        await _deviceRepository.SaveChangesAsync(cancellationToken);

        return Result<RejectDeviceRegistrationResponse>.Success(new RejectDeviceRegistrationResponse(
            device.Id.Value,
            device.RegistrationStatus.ToString(),
            command.Reason));
    }
}
