using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Authentication;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

/// <summary>
/// Command to register a new device.
/// </summary>
public record RegisterDeviceCommand(
    Guid TenantId,
    Guid DeviceId,
    string Name,
    string? RegistrationToken = null,
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
    private readonly IDeviceRegistrationTokenRepository _tokenRepository;
    private readonly IRegistrationTokenService _tokenService;

    public RegisterDeviceHandler(
        IDeviceRepository deviceRepository,
        IDeviceRegistrationTokenRepository tokenRepository,
        IRegistrationTokenService tokenService)
    {
        _deviceRepository = deviceRepository;
        _tokenRepository = tokenRepository;
        _tokenService = tokenService;
    }

    public async Task<Result<RegisterDeviceResponse>> Handle(
        RegisterDeviceCommand command,
        CancellationToken cancellationToken)
    {
        var deviceId = new DeviceId(command.DeviceId);
        var tenantId = new TenantId(command.TenantId);

        // Validate registration token if provided
        if (!string.IsNullOrEmpty(command.RegistrationToken))
        {
            var tokenValidation = await ValidateRegistrationTokenAsync(
                command.RegistrationToken,
                tenantId,
                deviceId,
                cancellationToken);

            if (tokenValidation.IsFailure)
            {
                return Result.Failure<RegisterDeviceResponse>(tokenValidation.Error!);
            }
        }

        // Check if device already exists
        var existingDevice = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);

        if (existingDevice is not null)
        {
            var error = Error.Conflict(
                "DEVICE_ALREADY_EXISTS",
                $"Device with ID {command.DeviceId} already exists.");
            return Result.Failure<RegisterDeviceResponse>(error);
        }

        // Create new device using factory method
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
            device.RegistrationStatus.ToString(),
            device.RegisteredAt));
    }

    private async Task<Result> ValidateRegistrationTokenAsync(
        string tokenString,
        TenantId tenantId,
        DeviceId deviceId,
        CancellationToken cancellationToken)
    {
        // Extract token prefix (format: sbt_{prefix}_{secret})
        var parts = tokenString.Split('_');
        if (parts.Length != 3 || parts[0] != "sbt")
        {
            return Result.Failure(Error.Validation(
                "RegistrationToken.InvalidFormat",
                "Registration token has invalid format."));
        }

        var tokenPrefix = $"sbt_{parts[1]}";

        // Find token in database by prefix
        var token = await _tokenRepository.GetByPrefixAsync(tokenPrefix, cancellationToken);

        if (token == null)
        {
            return Result.Failure(Error.NotFound(
                "RegistrationToken.NotFound",
                "Registration token not found or has expired."));
        }

        // Validate token belongs to correct tenant
        if (token.TenantId != tenantId)
        {
            return Result.Failure(Error.Validation(
                "RegistrationToken.InvalidTenant",
                "Registration token does not belong to this tenant."));
        }

        // Validate token hash
        if (!_tokenService.ValidateToken(tokenString, token.TokenHash))
        {
            return Result.Failure(Error.Validation(
                "RegistrationToken.Invalid",
                "Registration token is invalid."));
        }

        // Validate token is not used and not expired
        if (!token.IsValid)
        {
            return Result.Failure(Error.Validation(
                "RegistrationToken.Expired",
                "Registration token has expired or has already been used."));
        }

        // Mark token as used
        token.MarkAsUsed(deviceId);
        await _tokenRepository.UpdateAsync(token, cancellationToken);
        await _tokenRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
