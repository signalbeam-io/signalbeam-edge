using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

/// <summary>
/// Command to revoke a device API key.
/// </summary>
public record RevokeDeviceApiKeyCommand(Guid ApiKeyId);

/// <summary>
/// Response after revoking a device API key.
/// </summary>
public record RevokeDeviceApiKeyResponse(Guid ApiKeyId, DateTimeOffset RevokedAt);

/// <summary>
/// Handler for RevokeDeviceApiKeyCommand.
/// </summary>
public class RevokeDeviceApiKeyHandler
{
    private readonly IDeviceApiKeyRepository _apiKeyRepository;

    public RevokeDeviceApiKeyHandler(IDeviceApiKeyRepository apiKeyRepository)
    {
        _apiKeyRepository = apiKeyRepository;
    }

    public async Task<Result<RevokeDeviceApiKeyResponse>> Handle(
        RevokeDeviceApiKeyCommand command,
        CancellationToken cancellationToken)
    {
        // Find the API key - we need to add this method to the repository
        var apiKey = await _apiKeyRepository.GetByIdAsync(command.ApiKeyId, cancellationToken);

        if (apiKey == null)
        {
            var error = Error.NotFound(
                "API_KEY_NOT_FOUND",
                $"API key with ID {command.ApiKeyId} not found.");
            return Result.Failure<RevokeDeviceApiKeyResponse>(error);
        }

        // Revoke the key
        try
        {
            var revokedAt = DateTimeOffset.UtcNow;
            apiKey.Revoke(revokedAt);
            _apiKeyRepository.Update(apiKey);
            await _apiKeyRepository.SaveChangesAsync(cancellationToken);

            return Result<RevokeDeviceApiKeyResponse>.Success(new RevokeDeviceApiKeyResponse(
                apiKey.Id,
                revokedAt));
        }
        catch (InvalidOperationException ex)
        {
            var error = Error.Validation("REVOCATION_FAILED", ex.Message);
            return Result.Failure<RevokeDeviceApiKeyResponse>(error);
        }
    }
}
