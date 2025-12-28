using SignalBeam.Shared.Infrastructure.Results;
using System.Security.Cryptography.X509Certificates;

namespace SignalBeam.Shared.Infrastructure.Authentication;

/// <summary>
/// Validates device client certificates for mTLS authentication.
/// </summary>
public interface IDeviceCertificateValidator
{
    /// <summary>
    /// Validates a client certificate for device authentication.
    /// </summary>
    /// <param name="certificate">The client certificate to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing device authentication information if successful.</returns>
    Task<Result<DeviceAuthenticationResult>> ValidateAsync(
        X509Certificate2 certificate,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of device authentication (used by both API key and certificate validators).
/// </summary>
public record DeviceAuthenticationResult(
    Guid DeviceId,
    Guid TenantId,
    bool IsApproved,
    string DeviceStatus);
