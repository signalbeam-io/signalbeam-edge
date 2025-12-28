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

            // Update stored credentials if status changed
            if (status.Status != credentials.RegistrationStatus ||
                (status.ApiKey != null && status.ApiKey != credentials.ApiKey))
            {
                credentials.RegistrationStatus = status.Status;
                credentials.ApiKey = status.ApiKey;
                credentials.ApiKeyExpiresAt = status.ApiKeyExpiresAt;

                await _credentialsStore.SaveCredentialsAsync(credentials, cancellationToken);

                _logger.LogInformation(
                    "Device {DeviceId} registration status updated to {Status}. API key: {HasApiKey}",
                    credentials.DeviceId,
                    status.Status,
                    status.ApiKey != null ? "Received" : "Not yet provided");
            }

            return Result<CheckRegistrationStatusResponse>.Success(
                new CheckRegistrationStatusResponse(
                    status.Status,
                    status.Status == "Approved",
                    status.ApiKey,
                    status.ApiKeyExpiresAt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check registration status");
            return Result.Failure<CheckRegistrationStatusResponse>(
                Error.Failure("CheckStatus.Failed", $"Failed to check registration status: {ex.Message}"));
        }
    }
}
