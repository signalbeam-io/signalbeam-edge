using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Shared.Infrastructure.Messaging;

namespace SignalBeam.DeviceManager.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that periodically checks for expiring device certificates
/// and publishes NATS notifications for renewal.
/// </summary>
public class CertificateExpirationCheckService : BackgroundService
{
    private readonly ILogger<CertificateExpirationCheckService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly CertificateExpirationCheckOptions _options;

    public CertificateExpirationCheckService(
        ILogger<CertificateExpirationCheckService> logger,
        IServiceProvider serviceProvider,
        IOptions<CertificateExpirationCheckOptions> options)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Certificate Expiration Check Service started. Check interval: {Interval} hours, Renewal threshold: {Threshold} days",
            _options.CheckIntervalHours,
            _options.RenewalThresholdDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckExpiringCertificatesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while checking for expiring certificates");
            }

            await Task.Delay(TimeSpan.FromHours(_options.CheckIntervalHours), stoppingToken);
        }

        _logger.LogInformation("Certificate Expiration Check Service stopped");
    }

    private async Task CheckExpiringCertificatesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var certificateRepository = scope.ServiceProvider.GetRequiredService<IDeviceCertificateRepository>();
        var messagePublisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

        foreach (var warningDays in _options.ExpirationWarningDays)
        {
            var expiringCerts = await certificateRepository.GetExpiringCertificatesAsync(
                warningDays,
                cancellationToken);

            if (expiringCerts.Count == 0)
            {
                _logger.LogDebug("No certificates expiring within {Threshold} days", warningDays);
                continue;
            }

            _logger.LogWarning(
                "Found {Count} certificates expiring within {Threshold} days",
                expiringCerts.Count,
                warningDays);

            foreach (var cert in expiringCerts)
            {
                var daysUntilExpiration = (cert.ExpiresAt - DateTimeOffset.UtcNow).TotalDays;

                _logger.LogWarning(
                    "Certificate {SerialNumber} for device {DeviceId} expires in {DaysRemaining:F1} days on {ExpirationDate}",
                    cert.SerialNumber,
                    cert.DeviceId.Value,
                    daysUntilExpiration,
                    cert.ExpiresAt.ToString("yyyy-MM-dd"));

                var notification = new CertificateRenewalNotification(
                    cert.DeviceId.Value,
                    cert.SerialNumber,
                    cert.ExpiresAt,
                    (int)daysUntilExpiration,
                    $"/api/certificates/{cert.SerialNumber}/renew");

                try
                {
                    await messagePublisher.PublishAsync(
                        $"signalbeam.devices.certificates.renewal-required.{cert.DeviceId.Value}",
                        notification,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to publish renewal notification for certificate {SerialNumber}",
                        cert.SerialNumber);
                }
            }
        }
    }
}

/// <summary>
/// NATS message payload for certificate renewal notifications.
/// </summary>
public record CertificateRenewalNotification(
    Guid DeviceId,
    string SerialNumber,
    DateTimeOffset ExpiresAt,
    int DaysUntilExpiration,
    string RenewalEndpointUrl);

/// <summary>
/// Configuration options for certificate expiration check service.
/// </summary>
public class CertificateExpirationCheckOptions
{
    public const string SectionName = "CertificateExpirationCheck";

    /// <summary>
    /// How often to check for expiring certificates (in hours). Default: 6 hours.
    /// </summary>
    public double CheckIntervalHours { get; set; } = 6.0;

    /// <summary>
    /// Certificates expiring within this many days are eligible for renewal. Default: 30 days.
    /// </summary>
    public int RenewalThresholdDays { get; set; } = 30;

    /// <summary>
    /// Warning thresholds in days. Notifications are sent at each level. Default: 30, 14, 7, 3, 1.
    /// </summary>
    public int[] ExpirationWarningDays { get; set; } = [30, 14, 7, 3, 1];

    /// <summary>
    /// Enable or disable the background service. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
