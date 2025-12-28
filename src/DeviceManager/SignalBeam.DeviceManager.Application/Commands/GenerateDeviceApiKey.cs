using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Authentication;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

/// <summary>
/// Command to generate a new API key for a device (for key rotation).
/// </summary>
public record GenerateDeviceApiKeyCommand(
    Guid DeviceId,
    int ExpirationDays = 90,
    bool RevokeExistingKeys = false);

/// <summary>
/// Response after generating a new device API key.
/// </summary>
public record GenerateDeviceApiKeyResponse(
    Guid DeviceId,
    string ApiKey,
    string KeyPrefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt);

/// <summary>
/// Handler for GenerateDeviceApiKeyCommand.
/// </summary>
public class GenerateDeviceApiKeyHandler
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceApiKeyRepository _apiKeyRepository;
    private readonly IDeviceApiKeyService _apiKeyService;

    public GenerateDeviceApiKeyHandler(
        IDeviceRepository deviceRepository,
        IDeviceApiKeyRepository apiKeyRepository,
        IDeviceApiKeyService apiKeyService)
    {
        _deviceRepository = deviceRepository;
        _apiKeyRepository = apiKeyRepository;
        _apiKeyService = apiKeyService;
    }

    public async Task<Result<GenerateDeviceApiKeyResponse>> Handle(
        GenerateDeviceApiKeyCommand command,
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
            return Result.Failure<GenerateDeviceApiKeyResponse>(error);
        }

        // Check if device is approved
        if (device.RegistrationStatus != DeviceRegistrationStatus.Approved)
        {
            var error = Error.Forbidden(
                "DEVICE_NOT_APPROVED",
                "Cannot generate API key for a device that is not approved.");
            return Result.Failure<GenerateDeviceApiKeyResponse>(error);
        }

        // Optionally revoke existing keys
        if (command.RevokeExistingKeys)
        {
            var existingKeys = await _apiKeyRepository.GetActiveByDeviceIdAsync(deviceId, cancellationToken);
            foreach (var existingKey in existingKeys)
            {
                existingKey.Revoke(DateTimeOffset.UtcNow);
                _apiKeyRepository.Update(existingKey);
            }
        }

        // Generate new API key
        var (plainTextKey, keyHash, keyPrefix) = _apiKeyService.GenerateApiKey(deviceId);

        // Set expiration
        var createdAt = DateTimeOffset.UtcNow;
        var expiresAt = command.ExpirationDays > 0
            ? createdAt.AddDays(command.ExpirationDays)
            : (DateTimeOffset?)null; // Never expires if ExpirationDays is 0 or negative

        // Create API key entity
        var apiKey = Domain.Entities.DeviceApiKey.Create(
            deviceId,
            keyHash,
            keyPrefix,
            createdAt,
            expiresAt,
            createdBy: "system");

        // Save changes
        await _apiKeyRepository.AddAsync(apiKey, cancellationToken);
        await _apiKeyRepository.SaveChangesAsync(cancellationToken);

        return Result<GenerateDeviceApiKeyResponse>.Success(new GenerateDeviceApiKeyResponse(
            device.Id.Value,
            plainTextKey,
            keyPrefix,
            createdAt,
            expiresAt));
    }
}
