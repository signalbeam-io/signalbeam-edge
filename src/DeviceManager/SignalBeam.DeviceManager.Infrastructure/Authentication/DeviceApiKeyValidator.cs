using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SignalBeam.DeviceManager.Infrastructure.Persistence;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Authentication;
using SignalBeam.Shared.Infrastructure.Http;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Infrastructure.Authentication;

/// <summary>
/// Database-backed validator for device API keys.
/// </summary>
public class DeviceApiKeyValidator : IDeviceApiKeyValidator
{
    private readonly DeviceDbContext _context;
    private readonly IDeviceApiKeyService _apiKeyService;
    private readonly IHttpContextInfoProvider _httpContextInfo;
    private readonly ILogger<DeviceApiKeyValidator> _logger;

    public DeviceApiKeyValidator(
        DeviceDbContext context,
        IDeviceApiKeyService apiKeyService,
        IHttpContextInfoProvider httpContextInfo,
        ILogger<DeviceApiKeyValidator> logger)
    {
        _context = context;
        _apiKeyService = apiKeyService;
        _httpContextInfo = httpContextInfo;
        _logger = logger;
    }

    public async Task<Result<DeviceApiKeyValidationResult>> ValidateAsync(
        string apiKey,
        string keyPrefix,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var error = Error.Validation("INVALID_API_KEY", "API key cannot be empty.");
            return Result.Failure<DeviceApiKeyValidationResult>(error);
        }

        var now = DateTimeOffset.UtcNow;

        // Find the API key by prefix (first check for active, non-expired keys)
        var storedKey = await _context.DeviceApiKeys
            .Where(k => k.KeyPrefix == keyPrefix)
            .Where(k => k.RevokedAt == null) // Not revoked
            .Where(k => k.ExpiresAt == null || k.ExpiresAt > now) // Not expired
            .FirstOrDefaultAsync(cancellationToken);

        if (storedKey == null)
        {
            _logger.LogWarning("No active API key found with prefix {KeyPrefix}", keyPrefix);

            // Log failed authentication attempt
            await LogAuthenticationFailureAsync(null, "API key not found or expired", keyPrefix, cancellationToken);

            var error = Error.Unauthorized("INVALID_API_KEY", "The provided API key is not valid.");
            return Result.Failure<DeviceApiKeyValidationResult>(error);
        }

        // Validate the API key hash
        if (!_apiKeyService.ValidateApiKey(apiKey, storedKey.KeyHash))
        {
            _logger.LogWarning(
                "API key validation failed for device {DeviceId}: Hash mismatch",
                storedKey.DeviceId.Value);

            await LogAuthenticationFailureAsync(storedKey.DeviceId, "Invalid API key hash", keyPrefix, cancellationToken);

            var error = Error.Unauthorized("INVALID_API_KEY", "The provided API key is not valid.");
            return Result.Failure<DeviceApiKeyValidationResult>(error);
        }

        // Get the device to check registration status
        var device = await _context.Devices
            .Where(d => d.Id == storedKey.DeviceId)
            .FirstOrDefaultAsync(cancellationToken);

        if (device == null)
        {
            _logger.LogError("Device {DeviceId} not found for valid API key", storedKey.DeviceId.Value);

            await LogAuthenticationFailureAsync(storedKey.DeviceId, "Device not found", keyPrefix, cancellationToken);

            var error = Error.NotFound("DEVICE_NOT_FOUND", "The device associated with this API key was not found.");
            return Result.Failure<DeviceApiKeyValidationResult>(error);
        }

        // Check if device registration is approved
        if (device.RegistrationStatus != DeviceRegistrationStatus.Approved)
        {
            _logger.LogWarning(
                "Device {DeviceId} attempted authentication but registration status is {Status}",
                device.Id.Value,
                device.RegistrationStatus);

            await LogAuthenticationFailureAsync(
                storedKey.DeviceId,
                $"Device registration not approved: {device.RegistrationStatus}",
                keyPrefix,
                cancellationToken);

            var error = Error.Forbidden(
                "DEVICE_NOT_APPROVED",
                $"Device registration is {device.RegistrationStatus.ToString().ToLowerInvariant()}. Contact administrator.");
            return Result.Failure<DeviceApiKeyValidationResult>(error);
        }

        // Update last used timestamp
        storedKey.RecordUsage(now);
        await _context.SaveChangesAsync(cancellationToken);

        // Log successful authentication
        await LogAuthenticationSuccessAsync(storedKey.DeviceId, keyPrefix, cancellationToken);

        _logger.LogInformation(
            "Device {DeviceId} authenticated successfully using API key with prefix {KeyPrefix}",
            device.Id.Value,
            keyPrefix);

        var result = new DeviceApiKeyValidationResult
        {
            DeviceId = device.Id.Value,
            TenantId = device.TenantId.Value,
            IsApproved = device.RegistrationStatus == DeviceRegistrationStatus.Approved,
            DeviceStatus = device.Status.ToString()
        };

        return Result<DeviceApiKeyValidationResult>.Success(result);
    }

    private async Task LogAuthenticationSuccessAsync(
        DeviceId deviceId,
        string keyPrefix,
        CancellationToken cancellationToken)
    {
        try
        {
            var ipAddress = _httpContextInfo.GetClientIpAddress();
            var userAgent = _httpContextInfo.GetUserAgent();

            var log = Domain.Entities.DeviceAuthenticationLog.LogSuccess(
                deviceId,
                ipAddress,
                userAgent,
                DateTimeOffset.UtcNow,
                keyPrefix);

            await _context.DeviceAuthenticationLogs.AddAsync(log, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug(
                "Logged successful authentication for device {DeviceId} from IP {IpAddress}",
                deviceId.Value,
                ipAddress ?? "unknown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log successful authentication for device {DeviceId}", deviceId.Value);
        }
    }

    private async Task LogAuthenticationFailureAsync(
        DeviceId? deviceId,
        string failureReason,
        string? keyPrefix,
        CancellationToken cancellationToken)
    {
        try
        {
            var ipAddress = _httpContextInfo.GetClientIpAddress();
            var userAgent = _httpContextInfo.GetUserAgent();

            var log = Domain.Entities.DeviceAuthenticationLog.LogFailure(
                deviceId,
                ipAddress,
                userAgent,
                DateTimeOffset.UtcNow,
                failureReason,
                keyPrefix);

            await _context.DeviceAuthenticationLogs.AddAsync(log, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug(
                "Logged failed authentication attempt from IP {IpAddress}: {Reason}",
                ipAddress ?? "unknown",
                failureReason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log authentication failure");
        }
    }
}
