using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Queries;

/// <summary>
/// Query to get device registration status and API key (if approved).
/// </summary>
public record GetRegistrationStatusQuery(Guid DeviceId);

/// <summary>
/// Response containing registration status and API key.
/// </summary>
public record GetRegistrationStatusResponse(
    string Status,
    string? ApiKey = null,
    DateTimeOffset? ApiKeyExpiresAt = null);

/// <summary>
/// Handler for GetRegistrationStatusQuery.
/// </summary>
public class GetRegistrationStatusHandler
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceApiKeyRepository _apiKeyRepository;

    public GetRegistrationStatusHandler(
        IDeviceRepository deviceRepository,
        IDeviceApiKeyRepository apiKeyRepository)
    {
        _deviceRepository = deviceRepository;
        _apiKeyRepository = apiKeyRepository;
    }

    public async Task<Result<GetRegistrationStatusResponse>> Handle(
        GetRegistrationStatusQuery query,
        CancellationToken cancellationToken)
    {
        var deviceId = new DeviceId(query.DeviceId);

        // Get device
        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);

        if (device == null)
        {
            var error = Error.NotFound(
                "DEVICE_NOT_FOUND",
                $"Device with ID {query.DeviceId} not found.");
            return Result.Failure<GetRegistrationStatusResponse>(error);
        }

        // If approved, check for active API keys
        // NOTE: For security, we don't return the actual API key here.
        // The API key is only shown once when:
        // 1. Device registration is approved (admin receives it)
        // 2. A new API key is generated (during rotation)
        //
        // The edge agent should either:
        // - Use the API key received during initial approval (manual provisioning)
        // - Or implement a secure device approval callback/webhook
        //
        // This endpoint just indicates if the device is approved and has keys
        DateTimeOffset? expiresAt = null;

        if (device.RegistrationStatus == DeviceRegistrationStatus.Approved)
        {
            var activeKeys = await _apiKeyRepository.GetActiveByDeviceIdAsync(deviceId, cancellationToken);
            var latestKey = activeKeys.OrderByDescending(k => k.CreatedAt).FirstOrDefault();

            if (latestKey != null)
            {
                expiresAt = latestKey.ExpiresAt;
            }
        }

        return Result<GetRegistrationStatusResponse>.Success(new GetRegistrationStatusResponse(
            device.RegistrationStatus.ToString(),
            null, // API key not returned for security
            expiresAt));
    }
}
