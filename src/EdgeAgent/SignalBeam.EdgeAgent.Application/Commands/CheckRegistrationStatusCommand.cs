using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.EdgeAgent.Application.Commands;

/// <summary>
/// Command to check the registration status of the device.
/// Used to poll for approval and retrieve API key once approved.
/// </summary>
public record CheckRegistrationStatusCommand;

public record CheckRegistrationStatusResponse(
    string Status,
    bool IsApproved,
    string? ApiKey = null,
    DateTimeOffset? ApiKeyExpiresAt = null);

public class CheckRegistrationStatusCommandHandler
{
    private readonly ICloudClient _cloudClient;
    private readonly IDeviceCredentialsStore _credentialsStore;
    private readonly ILogger<CheckRegistrationStatusCommandHandler> _logger;

    public CheckRegistrationStatusCommandHandler(
        ICloudClient cloudClient,
        IDeviceCredentialsStore credentialsStore,
        ILogger<CheckRegistrationStatusCommandHandler> logger)
    {
        _cloudClient = cloudClient;
        _credentialsStore = credentialsStore;
        _logger = logger;
    }

    public async Task<Result<CheckRegistrationStatusResponse>> Handle(
        CheckRegistrationStatusCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            // Load stored credentials
            var credentials = await _credentialsStore.LoadCredentialsAsync(cancellationToken);
            if (credentials == null)
            {
                return Result.Failure<CheckRegistrationStatusResponse>(
                    Error.Validation("NotRegistered", "Device is not registered. Please register first."));
            }

            // If already approved with API key, return current status
            if (credentials.RegistrationStatus == "Approved" && credentials.ApiKey != null)
            {
                _logger.LogDebug(
                    "Device {DeviceId} is already approved with API key",
                    credentials.DeviceId);

                return Result<CheckRegistrationStatusResponse>.Success(
                    new CheckRegistrationStatusResponse(
                        credentials.RegistrationStatus,
                        true,
                        credentials.ApiKey,
                        credentials.ApiKeyExpiresAt));
            }

            // Fetch current registration status from cloud
            var status = await _cloudClient.CheckRegistrationStatusAsync(credentials.DeviceId, cancellationToken);

            var apiKey = credentials.ApiKey ?? status.ApiKey;
            var apiKeyExpiresAt = credentials.ApiKeyExpiresAt ?? status.ApiKeyExpiresAt;

            // Once approved, claim the API key exactly once using the retained registration token.
            if (status.Status == "Approved" &&
                apiKey == null &&
                status.KeyClaimAvailable &&
                !string.IsNullOrEmpty(credentials.RegistrationToken))
            {
                try
                {
                    var claimed = await _cloudClient.ClaimApiKeyAsync(
                        credentials.DeviceId,
                        credentials.RegistrationToken!,
                        cancellationToken);

                    apiKey = claimed.ApiKey;
                    apiKeyExpiresAt = claimed.ExpiresAt;

                    _logger.LogInformation(
                        "Claimed API key for device {DeviceId}",
                        credentials.DeviceId);
                }
                catch (Exception ex)
                {
                    // Transient failures shouldn't abort the poll — the next cycle retries.
                    _logger.LogWarning(
                        ex,
                        "Failed to claim API key for device {DeviceId}; will retry on next poll",
                        credentials.DeviceId);
                }
            }

            // Persist any change to status or the newly claimed key.
            if (status.Status != credentials.RegistrationStatus || apiKey != credentials.ApiKey)
            {
                credentials.RegistrationStatus = status.Status;
                credentials.ApiKey = apiKey;
                credentials.ApiKeyExpiresAt = apiKeyExpiresAt;

                await _credentialsStore.SaveCredentialsAsync(credentials, cancellationToken);

                _logger.LogInformation(
                    "Device {DeviceId} registration status updated to {Status}. API key: {HasApiKey}",
                    credentials.DeviceId,
                    status.Status,
                    apiKey != null ? "Received" : "Not yet provided");
            }

            return Result<CheckRegistrationStatusResponse>.Success(
                new CheckRegistrationStatusResponse(
                    status.Status,
                    status.Status == "Approved",
                    apiKey,
                    apiKeyExpiresAt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check registration status");
            return Result.Failure<CheckRegistrationStatusResponse>(
                Error.Failure("CheckStatus.Failed", $"Failed to check registration status: {ex.Message}"));
        }
    }
}
