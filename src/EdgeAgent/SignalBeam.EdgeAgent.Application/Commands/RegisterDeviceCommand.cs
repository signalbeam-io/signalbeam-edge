using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Models;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.EdgeAgent.Application.Commands;

public record RegisterDeviceCommand(
    Guid TenantId,
    string DeviceId,
    string RegistrationToken,
    string? Hostname = null,
    string? Platform = null);

public class RegisterDeviceCommandHandler
{
    private readonly ICloudClient _cloudClient;
    private readonly IDeviceCredentialsStore _credentialsStore;
    private readonly ILogger<RegisterDeviceCommandHandler> _logger;

    public RegisterDeviceCommandHandler(
        ICloudClient cloudClient,
        IDeviceCredentialsStore credentialsStore,
        ILogger<RegisterDeviceCommandHandler> logger)
    {
        _cloudClient = cloudClient;
        _credentialsStore = credentialsStore;
        _logger = logger;
    }

    public async Task<Result<DeviceRegistrationResponse>> Handle(
        RegisterDeviceCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check if device is already registered
            var existingCredentials = await _credentialsStore.LoadCredentialsAsync(cancellationToken);
            if (existingCredentials != null)
            {
                _logger.LogWarning(
                    "Device {DeviceId} is already registered with status {Status}",
                    existingCredentials.DeviceId,
                    existingCredentials.RegistrationStatus);

                // Return existing registration status
                return Result<DeviceRegistrationResponse>.Success(new DeviceRegistrationResponse(
                    existingCredentials.DeviceId,
                    existingCredentials.DeviceName ?? "Unknown",
                    existingCredentials.RegistrationStatus,
                    existingCredentials.RegisteredAt,
                    existingCredentials.ApiKey,
                    existingCredentials.ApiKeyExpiresAt));
            }

            var request = new DeviceRegistrationRequest(
                command.TenantId,
                command.DeviceId,
                command.RegistrationToken,
                command.Hostname,
                command.Platform);

            var response = await _cloudClient.RegisterDeviceAsync(request, cancellationToken);

            // Save device credentials locally
            var credentials = new DeviceCredentials
            {
                DeviceId = response.DeviceId,
                TenantId = command.TenantId,
                DeviceName = response.Name,
                RegistrationStatus = response.Status,
                RegisteredAt = response.RegisteredAt,
                ApiKey = response.ApiKey,
                ApiKeyExpiresAt = response.ApiKeyExpiresAt
            };

            await _credentialsStore.SaveCredentialsAsync(credentials, cancellationToken);

            _logger.LogInformation(
                "Device {DeviceId} registered successfully with status {Status}. API key: {HasApiKey}",
                response.DeviceId,
                response.Status,
                response.ApiKey != null ? "Provided" : "Pending approval");

            return Result<DeviceRegistrationResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register device");
            return Result.Failure<DeviceRegistrationResponse>(
                Error.Failure("DeviceRegistration.Failed", $"Failed to register device: {ex.Message}"));
        }
    }
}
