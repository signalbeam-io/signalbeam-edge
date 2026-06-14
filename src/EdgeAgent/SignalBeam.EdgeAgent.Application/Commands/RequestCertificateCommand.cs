using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.EdgeAgent.Application.Commands;

/// <summary>
/// Provisions an mTLS client certificate: generates a key pair and CSR locally, has the cloud CA
/// sign it, stores the certificate material, and records it in the device credentials. The private
/// key never leaves the device.
/// </summary>
public record RequestCertificateCommand;

public record RequestCertificateResponse(
    string SerialNumber,
    DateTimeOffset ExpiresAt,
    string CertificatePath);

public class RequestCertificateCommandHandler
{
    private readonly ICloudClient _cloudClient;
    private readonly ICsrGenerator _csrGenerator;
    private readonly ICertificateStore _certificateStore;
    private readonly IDeviceCredentialsStore _credentialsStore;
    private readonly ILogger<RequestCertificateCommandHandler> _logger;

    public RequestCertificateCommandHandler(
        ICloudClient cloudClient,
        ICsrGenerator csrGenerator,
        ICertificateStore certificateStore,
        IDeviceCredentialsStore credentialsStore,
        ILogger<RequestCertificateCommandHandler> logger)
    {
        _cloudClient = cloudClient;
        _csrGenerator = csrGenerator;
        _certificateStore = certificateStore;
        _credentialsStore = credentialsStore;
        _logger = logger;
    }

    public async Task<Result<RequestCertificateResponse>> Handle(
        RequestCertificateCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var credentials = await _credentialsStore.LoadCredentialsAsync(cancellationToken);
            if (credentials is null)
            {
                return Result.Failure<RequestCertificateResponse>(
                    Error.Validation("NotRegistered", "Device is not registered. Please register first."));
            }

            // The CSR request is authenticated by the device API key, so a key must exist.
            if (string.IsNullOrEmpty(credentials.ApiKey))
            {
                return Result.Failure<RequestCertificateResponse>(
                    Error.Validation("NoApiKey", "Device has no API key yet; cannot request a certificate."));
            }

            var subject = $"CN=device-{credentials.DeviceId}, O=SignalBeam";
            var (csrPem, privateKeyPem) = _csrGenerator.GenerateCsr(subject);

            var bundle = await _cloudClient.RequestCertificateAsync(credentials.DeviceId, csrPem, cancellationToken);

            var paths = await _certificateStore.SaveAsync(
                bundle.CertificatePem,
                privateKeyPem,
                bundle.CaCertificatePem,
                cancellationToken);

            credentials.ClientCertificatePath = paths.CertificatePath;
            credentials.ClientPrivateKeyPath = paths.PrivateKeyPath;
            credentials.CaCertificatePath = paths.CaCertificatePath;
            credentials.CertificateSerialNumber = bundle.SerialNumber;
            credentials.CertificateExpiresAt = bundle.ExpiresAt;

            await _credentialsStore.SaveCredentialsAsync(credentials, cancellationToken);

            _logger.LogInformation(
                "Provisioned mTLS certificate for device {DeviceId}; serial {Serial}, expires {Expiry:O}",
                credentials.DeviceId, bundle.SerialNumber, bundle.ExpiresAt);

            return Result<RequestCertificateResponse>.Success(
                new RequestCertificateResponse(bundle.SerialNumber, bundle.ExpiresAt, paths.CertificatePath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to provision certificate");
            return Result.Failure<RequestCertificateResponse>(
                Error.Failure("RequestCertificate.Failed", $"Failed to provision certificate: {ex.Message}"));
        }
    }
}
