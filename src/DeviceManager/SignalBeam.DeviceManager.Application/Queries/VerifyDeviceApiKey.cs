using SignalBeam.Shared.Infrastructure.Authentication;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Queries;

public record VerifyDeviceApiKeyQuery(string ApiKey);

public record VerifyDeviceApiKeyResponse(Guid DeviceId, Guid TenantId);

// Service-to-service check used by BundleOrchestrator, which serves device endpoints but has no
// access to device keys. Same rules as DeviceManager's own device authentication: active key,
// matching hash, approved device.
public class VerifyDeviceApiKeyHandler
{
    private readonly IDeviceApiKeyService _apiKeyService;
    private readonly IDeviceApiKeyValidator _apiKeyValidator;

    public VerifyDeviceApiKeyHandler(IDeviceApiKeyService apiKeyService, IDeviceApiKeyValidator apiKeyValidator)
    {
        _apiKeyService = apiKeyService;
        _apiKeyValidator = apiKeyValidator;
    }

    public async Task<Result<VerifyDeviceApiKeyResponse>> Handle(VerifyDeviceApiKeyQuery query, CancellationToken cancellationToken)
    {
        var keyPrefix = string.IsNullOrWhiteSpace(query.ApiKey) ? null : _apiKeyService.ExtractKeyPrefix(query.ApiKey);
        if (string.IsNullOrWhiteSpace(keyPrefix))
        {
            return Result.Failure<VerifyDeviceApiKeyResponse>(
                Error.Unauthorized("INVALID_API_KEY", "The provided API key is not valid."));
        }

        var result = await _apiKeyValidator.ValidateAsync(query.ApiKey, keyPrefix, cancellationToken);
        if (result.IsFailure)
        {
            // A key whose device has gone missing is just an invalid key to the caller.
            var error = result.Error!.Type == ErrorType.Forbidden
                ? result.Error
                : Error.Unauthorized("INVALID_API_KEY", "The provided API key is not valid.");
            return Result.Failure<VerifyDeviceApiKeyResponse>(error);
        }

        return Result<VerifyDeviceApiKeyResponse>.Success(
            new VerifyDeviceApiKeyResponse(result.Value!.DeviceId, result.Value.TenantId));
    }
}
