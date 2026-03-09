using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.EdgeAgent.Application.Models;
using SignalBeam.EdgeAgent.Application.Services;

namespace SignalBeam.EdgeAgent.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that checks certificate expiration and automatically renews
/// certificates approaching expiration.
/// </summary>
public class CertificateRenewalBackgroundService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<CertificateRenewalBackgroundService> _logger;
    private readonly IDeviceCredentialsStore _credentialsStore;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CertificateRenewalOptions _options;

    public CertificateRenewalBackgroundService(
        ILogger<CertificateRenewalBackgroundService> logger,
        IDeviceCredentialsStore credentialsStore,
        IHttpClientFactory httpClientFactory,
        IOptions<CertificateRenewalOptions> options)
    {
        _logger = logger;
        _credentialsStore = credentialsStore;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Certificate Renewal Service started. Check interval: {Interval} hours, Renewal threshold: {Threshold} days",
            _options.CheckIntervalHours,
            _options.RenewalThresholdDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndRenewCertificateAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during certificate renewal check");
            }

            await Task.Delay(TimeSpan.FromHours(_options.CheckIntervalHours), stoppingToken);
        }

        _logger.LogInformation("Certificate Renewal Service stopped");
    }

    private async Task CheckAndRenewCertificateAsync(CancellationToken cancellationToken)
    {
        var credentials = await _credentialsStore.LoadCredentialsAsync(cancellationToken);
        if (credentials == null)
        {
            _logger.LogDebug("No credentials found, skipping certificate renewal check");
            return;
        }

        if (string.IsNullOrEmpty(credentials.ClientCertificatePath)
            || string.IsNullOrEmpty(credentials.CertificateSerialNumber))
        {
            _logger.LogDebug("No certificate configured, skipping renewal check");
            return;
        }

        if (!credentials.CertificateExpiresAt.HasValue)
        {
            _logger.LogDebug("Certificate expiration not tracked, skipping renewal check");
            return;
        }

        var daysUntilExpiration = (credentials.CertificateExpiresAt.Value - DateTimeOffset.UtcNow).TotalDays;

        if (daysUntilExpiration > _options.RenewalThresholdDays)
        {
            _logger.LogDebug(
                "Certificate expires in {DaysRemaining:F1} days, no renewal needed (threshold: {Threshold} days)",
                daysUntilExpiration,
                _options.RenewalThresholdDays);
            return;
        }

        _logger.LogInformation(
            "Certificate {SerialNumber} expires in {DaysRemaining:F1} days, initiating renewal",
            credentials.CertificateSerialNumber,
            daysUntilExpiration);

        await RenewCertificateAsync(credentials, cancellationToken);
    }

    private async Task RenewCertificateAsync(
        DeviceCredentials credentials,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("CloudClient");

        var response = await client.PostAsync(
            $"/api/certificates/{credentials.CertificateSerialNumber}/renew",
            null,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Certificate renewal failed for {SerialNumber}: {StatusCode} {Content}",
                credentials.CertificateSerialNumber,
                response.StatusCode,
                content);
            return;
        }

        var renewResponse = await response.Content.ReadFromJsonAsync<CertificateRenewResponse>(
            JsonOptions,
            cancellationToken);

        if (renewResponse == null)
        {
            _logger.LogError("Certificate renewal returned empty response");
            return;
        }

        // Save new certificate files atomically (write to temp, then move)
        await SaveCertificateFilesAsync(credentials, renewResponse);

        // Update credentials with new certificate info
        credentials.CertificateSerialNumber = renewResponse.SerialNumber;
        credentials.CertificateExpiresAt = renewResponse.ExpiresAt;

        await _credentialsStore.SaveCredentialsAsync(credentials, cancellationToken);

        _logger.LogInformation(
            "Certificate renewed successfully. New serial: {SerialNumber}, expires: {ExpiresAt:O}",
            renewResponse.SerialNumber,
            renewResponse.ExpiresAt);
    }

    private static async Task SaveCertificateFilesAsync(
        DeviceCredentials credentials,
        CertificateRenewResponse renewResponse)
    {
        if (string.IsNullOrEmpty(credentials.ClientCertificatePath)
            || string.IsNullOrEmpty(credentials.ClientPrivateKeyPath))
        {
            return;
        }

        // Write to temp files first, then move for atomicity
        var certTempPath = credentials.ClientCertificatePath + ".tmp";
        var keyTempPath = credentials.ClientPrivateKeyPath + ".tmp";

        await File.WriteAllTextAsync(certTempPath, renewResponse.CertificatePem);
        await File.WriteAllTextAsync(keyTempPath, renewResponse.PrivateKeyPem);

        // Set restrictive permissions on private key (Unix only)
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(keyTempPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.SetUnixFileMode(certTempPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        // Atomic move
        File.Move(certTempPath, credentials.ClientCertificatePath, overwrite: true);
        File.Move(keyTempPath, credentials.ClientPrivateKeyPath, overwrite: true);

        // Update CA cert if provided
        if (!string.IsNullOrEmpty(credentials.CaCertificatePath)
            && !string.IsNullOrEmpty(renewResponse.CaCertificatePem))
        {
            var caTempPath = credentials.CaCertificatePath + ".tmp";
            await File.WriteAllTextAsync(caTempPath, renewResponse.CaCertificatePem);
            File.Move(caTempPath, credentials.CaCertificatePath, overwrite: true);
        }
    }

    private record CertificateRenewResponse(
        Guid DeviceId,
        string CertificatePem,
        string PrivateKeyPem,
        string CaCertificatePem,
        string SerialNumber,
        string Fingerprint,
        DateTimeOffset IssuedAt,
        DateTimeOffset ExpiresAt);
}

/// <summary>
/// Configuration options for certificate auto-renewal.
/// </summary>
public class CertificateRenewalOptions
{
    public const string SectionName = "Certificates:AutoRenewal";

    /// <summary>
    /// How often to check certificate expiration (in hours). Default: 12.
    /// </summary>
    public double CheckIntervalHours { get; set; } = 12.0;

    /// <summary>
    /// Renew certificates expiring within this many days. Default: 30.
    /// </summary>
    public int RenewalThresholdDays { get; set; } = 30;

    /// <summary>
    /// Enable or disable auto-renewal. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
