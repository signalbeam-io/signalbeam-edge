using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

public record ReportDeviceStateCommand(
    Guid DeviceId,
    BundleDeploymentStatus? BundleDeploymentStatus,
    DateTimeOffset Timestamp);

public record ReportDeviceStateResponse(
    Guid DeviceId,
    BundleDeploymentStatus? BundleDeploymentStatus,
    DateTimeOffset ReportedAt);

/// <summary>
/// Handler for reporting device state from the edge agent.
/// Updates bundle deployment status based on agent feedback.
/// </summary>
public class ReportDeviceStateHandler
{
    private readonly IDeviceRepository _deviceRepository;

    public ReportDeviceStateHandler(IDeviceRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<Result<ReportDeviceStateResponse>> Handle(
        ReportDeviceStateCommand command,
        CancellationToken cancellationToken)
    {
        var deviceId = new DeviceId(command.DeviceId);
        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);

        if (device is null)
        {
            var error = Error.NotFound("DEVICE_NOT_FOUND", $"Device with ID {command.DeviceId} not found.");
            return Result.Failure<ReportDeviceStateResponse>(error);
        }

        // Update bundle deployment status if provided
        if (command.BundleDeploymentStatus.HasValue)
        {
            device.UpdateBundleDeploymentStatus(command.BundleDeploymentStatus.Value, command.Timestamp);
        }

        await _deviceRepository.SaveChangesAsync(cancellationToken);

        return Result<ReportDeviceStateResponse>.Success(new ReportDeviceStateResponse(
            DeviceId: device.Id.Value,
            BundleDeploymentStatus: device.BundleDeploymentStatus,
            ReportedAt: command.Timestamp));
    }
}
