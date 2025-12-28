using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Services;

namespace SignalBeam.EdgeAgent.Infrastructure.Cloud;

/// <summary>
/// HTTP message handler that adds the device API key to outgoing requests.
/// </summary>
public class DeviceApiKeyHandler : DelegatingHandler
{
    private readonly IDeviceCredentialsStore _credentialsStore;
    private readonly ILogger<DeviceApiKeyHandler> _logger;

    public DeviceApiKeyHandler(
        IDeviceCredentialsStore credentialsStore,
        ILogger<DeviceApiKeyHandler> logger)
    {
        _credentialsStore = credentialsStore;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Load device credentials
        var credentials = await _credentialsStore.LoadCredentialsAsync(cancellationToken);

        // Add API key to request header if available
        if (credentials?.ApiKey != null)
        {
            request.Headers.Add("X-API-Key", credentials.ApiKey);
            _logger.LogDebug("Added device API key to request: {Method} {Uri}", request.Method, request.RequestUri);

            // Check if API key is expired or expiring soon
            if (credentials.ApiKeyExpiresAt.HasValue)
            {
                var daysUntilExpiration = (credentials.ApiKeyExpiresAt.Value - DateTimeOffset.UtcNow).TotalDays;

                if (daysUntilExpiration <= 0)
                {
                    _logger.LogWarning(
                        "Device API key has expired on {ExpiryDate}. Please rotate the key.",
                        credentials.ApiKeyExpiresAt.Value);
                }
                else if (daysUntilExpiration <= 7)
                {
                    _logger.LogWarning(
                        "Device API key expires in {Days} days on {ExpiryDate}. Consider rotating the key.",
                        (int)daysUntilExpiration,
                        credentials.ApiKeyExpiresAt.Value);
                }
            }
        }
        else
        {
            _logger.LogWarning(
                "No device API key available. Request may fail if authentication is required. " +
                "Device registration status: {Status}",
                credentials?.RegistrationStatus ?? "Unknown");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
