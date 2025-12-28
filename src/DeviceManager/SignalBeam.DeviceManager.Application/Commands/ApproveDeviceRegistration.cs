using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Authentication;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

/// <summary>
/// Command to approve a pending device registration and generate API key.
/// </summary>
public record ApproveDeviceRegistrationCommand(Guid DeviceId);

/// <summary>
/// Response after approving device registration.
/// </summary>
public record ApproveDeviceRegistrationResponse(
    Guid DeviceId,
    string ApiKey,
    DateTimeOffset? ExpiresAt);

/// <summary>
/// Handler for ApproveDeviceRegistrationCommand.
/// </summary>
public class ApproveDeviceRegistrationHandler
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceApiKeyRepository _apiKeyRepository;
    private readonly IDeviceApiKeyService _apiKeyService;

    public ApproveDeviceRegistrationHandler(
        IDeviceRepository deviceRepository,
        IDeviceApiKeyRepository apiKeyRepository,
        IDeviceApiKeyService apiKeyService)
    {
        _deviceRepository = deviceRepository;
        _apiKeyRepository = apiKeyRepository;
        _apiKeyService = apiKeyService;
    }

    public async Task<Result<ApproveDeviceRegistrationResponse>> Handle(
        ApproveDeviceRegistrationCommand command,
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
            return Result.Failure<ApproveDeviceRegistrationResponse>(error);
        }

        // Approve the registration
        try
        {
            device.ApproveRegistration(DateTimeOffset.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            var error = Error.Validation("APPROVAL_FAILED", ex.Message);
            return Result.Failure<ApproveDeviceRegistrationResponse>(error);
        }

        // Generate API key
        var (plainTextKey, keyHash, keyPrefix) = _apiKeyService.GenerateApiKey(deviceId);

        // Set expiration (e.g., 90 days from now, configurable)
        var expiresAt = DateTimeOffset.UtcNow.AddDays(90);

        // Create API key entity
        var apiKey = Domain.Entities.DeviceApiKey.Create(
            deviceId,
            keyHash,
            keyPrefix,
            DateTimeOffset.UtcNow,
            expiresAt,
            createdBy: "system");

        // Save changes
        await _apiKeyRepository.AddAsync(apiKey, cancellationToken);
        await _deviceRepository.SaveChangesAsync(cancellationToken);
        await _apiKeyRepository.SaveChangesAsync(cancellationToken);

        return Result<ApproveDeviceRegistrationResponse>.Success(new ApproveDeviceRegistrationResponse(
            device.Id.Value,
            plainTextKey,
            expiresAt));
    }
}
