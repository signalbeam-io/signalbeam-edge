using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

/// <summary>
/// Command to register a new device.
/// </summary>
public record RegisterDeviceCommand(
    Guid TenantId,
    Guid DeviceId,
    string Name,
    string? Metadata = null);

/// <summary>
/// Response after registering a device.
/// </summary>
public record RegisterDeviceResponse(
    Guid DeviceId,
    string Name,
    string Status,
    DateTimeOffset RegisteredAt);

/// <summary>
/// Handler for RegisterDeviceCommand.
/// Uses Wolverine's IMessageHandler pattern.
/// </summary>
public class RegisterDeviceHandler
{
    private readonly IDeviceRepository _deviceRepository;

    public RegisterDeviceHandler(IDeviceRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<Result<RegisterDeviceResponse>> Handle(
        RegisterDeviceCommand command,
        CancellationToken cancellationToken)
    {
        // Check if device already exists
        var deviceId = new DeviceId(command.DeviceId);
        var existingDevice = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);

        if (existingDevice is not null)
        {
            var error = Error.Conflict(
                "DEVICE_ALREADY_EXISTS",
                $"Device with ID {command.DeviceId} already exists.");
            return Result.Failure<RegisterDeviceResponse>(error);
        }

        // Create new device using factory method
        var tenantId = new TenantId(command.TenantId);
        var device = Device.Register(
            deviceId,
            tenantId,
            command.Name,
            DateTimeOffset.UtcNow,
            command.Metadata);

        // Save to repository
        await _deviceRepository.AddAsync(device, cancellationToken);
        await _deviceRepository.SaveChangesAsync(cancellationToken);

        // Return response
        return Result<RegisterDeviceResponse>.Success(new RegisterDeviceResponse(
            device.Id.Value,
            device.Name,
            device.Status.ToString(),
            device.RegisteredAt));
    }
}
