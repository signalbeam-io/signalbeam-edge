using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Authentication;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

/// <summary>
/// Command for a device to claim its API key exactly once after admin approval.
/// At this point in the lifecycle the device has no API key, so the claim is
/// authenticated by the registration token the device used to register — which is
/// BCrypt-verified and bound to this specific device.
/// </summary>
public record ClaimDeviceApiKeyCommand(Guid DeviceId, string RegistrationToken);

/// <summary>
/// Response carrying the freshly generated API key. The plaintext key is returned
/// only here and never again.
/// </summary>
public record ClaimDeviceApiKeyResponse(
    Guid DeviceId,
    string ApiKey,
    string KeyPrefix,
    DateTimeOffset? ExpiresAt);

/// <summary>
/// Handler for <see cref="ClaimDeviceApiKeyCommand"/>.
/// </summary>
public class ClaimDeviceApiKeyHandler
{
    private const int ApiKeyExpirationDays = 90;

    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceApiKeyRepository _apiKeyRepository;
    private readonly IDeviceRegistrationTokenRepository _tokenRepository;
    private readonly IDeviceApiKeyService _apiKeyService;
    private readonly IRegistrationTokenService _tokenService;

    public ClaimDeviceApiKeyHandler(
        IDeviceRepository deviceRepository,
        IDeviceApiKeyRepository apiKeyRepository,
        IDeviceRegistrationTokenRepository tokenRepository,
        IDeviceApiKeyService apiKeyService,
        IRegistrationTokenService tokenService)
    {
        _deviceRepository = deviceRepository;
        _apiKeyRepository = apiKeyRepository;
        _tokenRepository = tokenRepository;
        _apiKeyService = apiKeyService;
        _tokenService = tokenService;
    }

    public async Task<Result<ClaimDeviceApiKeyResponse>> Handle(
        ClaimDeviceApiKeyCommand command,
        CancellationToken cancellationToken)
    {
        var deviceId = new DeviceId(command.DeviceId);
        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);

        if (device is null)
        {
            return Result.Failure<ClaimDeviceApiKeyResponse>(Error.NotFound(
                "DEVICE_NOT_FOUND",
                $"Device with ID {command.DeviceId} not found."));
        }

        if (device.RegistrationStatus != DeviceRegistrationStatus.Approved)
        {
            return Result.Failure<ClaimDeviceApiKeyResponse>(Error.Forbidden(
                "DEVICE_NOT_APPROVED",
                "Device is not approved; an API key cannot be claimed yet."));
        }

        // One-time semantics: once claimed, force the device through key rotation instead.
        if (device.KeyClaimedAt is not null)
        {
            return Result.Failure<ClaimDeviceApiKeyResponse>(Error.Conflict(
                "KEY_ALREADY_CLAIMED",
                "The API key for this device has already been claimed. Use key rotation to obtain a new key."));
        }

        // Authenticate the claim with the registration token bound to this device.
        var tokenValidation = await ValidateClaimTokenAsync(command.RegistrationToken, device, cancellationToken);
        if (tokenValidation.IsFailure)
        {
            return Result.Failure<ClaimDeviceApiKeyResponse>(tokenValidation.Error!);
        }

        // Supersede any pre-existing active key (e.g. one generated for the admin at approval time)
        // so exactly one key — the device's claimed key — is active.
        var existingKeys = await _apiKeyRepository.GetActiveByDeviceIdAsync(deviceId, cancellationToken);
        foreach (var existingKey in existingKeys)
        {
            existingKey.Revoke(DateTimeOffset.UtcNow);
            _apiKeyRepository.Update(existingKey);
        }

        var (plainTextKey, keyHash, keyPrefix) = _apiKeyService.GenerateApiKey(deviceId);
        var createdAt = DateTimeOffset.UtcNow;
        var expiresAt = createdAt.AddDays(ApiKeyExpirationDays);

        var apiKey = DeviceApiKey.Create(
            deviceId,
            keyHash,
            keyPrefix,
            createdAt,
            expiresAt,
            createdBy: "device-claim");

        await _apiKeyRepository.AddAsync(apiKey, cancellationToken);

        device.MarkKeyClaimed(createdAt);

        // The device and API key repositories share one DbContext, so a single SaveChanges
        // commits the revoked old keys, the new key, and the KeyClaimedAt marker atomically —
        // the device can't end up marked-claimed without a usable key.
        await _deviceRepository.SaveChangesAsync(cancellationToken);

        return Result<ClaimDeviceApiKeyResponse>.Success(new ClaimDeviceApiKeyResponse(
            device.Id.Value,
            plainTextKey,
            keyPrefix,
            expiresAt));
    }

    private async Task<Result> ValidateClaimTokenAsync(
        string tokenString,
        Device device,
        CancellationToken cancellationToken)
    {
        // Expected format: sbt_{prefix}_{secret}
        if (string.IsNullOrWhiteSpace(tokenString))
        {
            return Result.Failure(Error.Unauthorized(
                "RegistrationToken.InvalidFormat",
                "Registration token is required."));
        }

        var parts = tokenString.Split('_');
        if (parts.Length != 3 || parts[0] != "sbt")
        {
            return Result.Failure(Error.Unauthorized(
                "RegistrationToken.InvalidFormat",
                "Registration token has invalid format."));
        }

        var tokenPrefix = $"sbt_{parts[1]}";
        var token = await _tokenRepository.GetByPrefixAsync(tokenPrefix, cancellationToken);

        if (token is null)
        {
            return Result.Failure(Error.Unauthorized(
                "RegistrationToken.Invalid",
                "Registration token is invalid."));
        }

        if (token.TenantId != device.TenantId)
        {
            return Result.Failure(Error.Unauthorized(
                "RegistrationToken.Invalid",
                "Registration token is invalid."));
        }

        if (!_tokenService.ValidateToken(tokenString, token.TokenHash))
        {
            return Result.Failure(Error.Unauthorized(
                "RegistrationToken.Invalid",
                "Registration token is invalid."));
        }

        // Bind the claim to the device that actually used this token during registration.
        if (token.UsedByDeviceId is null || token.UsedByDeviceId != device.Id)
        {
            return Result.Failure(Error.Unauthorized(
                "RegistrationToken.DeviceMismatch",
                "Registration token was not used by this device."));
        }

        // Honour explicit revocation and expiry. We do NOT use token.IsValid here because its
        // MaxUses check is already satisfied (consumed) by registration — the claim happens after.
        if (token.IsRevoked || token.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return Result.Failure(Error.Unauthorized(
                "RegistrationToken.Expired",
                "Registration token has been revoked or has expired."));
        }

        return Result.Success();
    }
}
