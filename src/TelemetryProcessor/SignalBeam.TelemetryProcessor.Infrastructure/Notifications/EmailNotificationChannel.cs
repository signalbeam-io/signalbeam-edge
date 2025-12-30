using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.TelemetryProcessor.Application.Services.Notifications;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Notifications;

/// <summary>
/// Email notification channel using SMTP.
/// </summary>
public class EmailNotificationChannel : INotificationChannel
{
    private readonly ILogger<EmailNotificationChannel> _logger;
    private readonly NotificationOptions _options;

    public NotificationChannel Channel => NotificationChannel.Email;
    public bool IsEnabled => _options.Channels.Email.Enabled;

    public EmailNotificationChannel(
        ILogger<EmailNotificationChannel> logger,
        IOptions<NotificationOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task<NotificationResult> SendAsync(
        Alert alert,
        string recipient,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.LogWarning("Email notifications are disabled");
            return NotificationResult.Failed("Email notifications are disabled");
        }

        var config = _options.Channels.Email;

        if (string.IsNullOrEmpty(config.SmtpServer))
        {
            _logger.LogError("SMTP server not configured");
            return NotificationResult.Failed("SMTP server not configured");
        }

        try
        {
            var subject = FormatSubject(alert);
            var body = FormatBody(alert);

            using var message = new MailMessage
            {
                From = new MailAddress(
                    config.FromAddress ?? "alerts@signalbeam.io",
                    config.FromName ?? "SignalBeam Alerts"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            message.To.Add(recipient);

            using var smtpClient = new SmtpClient(config.SmtpServer, config.SmtpPort)
            {
                EnableSsl = config.UseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };

            if (!string.IsNullOrEmpty(config.Username) && !string.IsNullOrEmpty(config.Password))
            {
                smtpClient.Credentials = new NetworkCredential(config.Username, config.Password);
            }

            await smtpClient.SendMailAsync(message, cancellationToken);

            _logger.LogInformation(
                "Email notification sent successfully to {Recipient} for alert {AlertId}",
                recipient,
                alert.Id);

            return NotificationResult.Succeeded(DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send email notification to {Recipient} for alert {AlertId}",
                recipient,
                alert.Id);

            return NotificationResult.Failed(ex.Message);
        }
    }

    private static string FormatSubject(Alert alert)
    {
        var severityEmoji = alert.Severity switch
        {
            AlertSeverity.Info => "ℹ️",
            AlertSeverity.Warning => "⚠️",
            AlertSeverity.Critical => "🚨",
            _ => "📢"
        };

        return $"{severityEmoji} [{alert.Severity}] {alert.Title}";
    }

    private static string FormatBody(Alert alert)
    {
        var severityColor = alert.Severity switch
        {
            AlertSeverity.Info => "#0066CC",
            AlertSeverity.Warning => "#FF9800",
            AlertSeverity.Critical => "#D32F2F",
            _ => "#757575"
        };

        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: {severityColor}; color: white; padding: 15px; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f9f9f9; padding: 20px; border: 1px solid #ddd; border-top: none; }}
        .field {{ margin-bottom: 15px; }}
        .label {{ font-weight: bold; color: #555; }}
        .value {{ margin-top: 5px; }}
        .footer {{ margin-top: 20px; padding-top: 20px; border-top: 1px solid #ddd; font-size: 12px; color: #777; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h2>{alert.Severity} Alert: {alert.Title}</h2>
        </div>
        <div class=""content"">
            <div class=""field"">
                <div class=""label"">Alert Type:</div>
                <div class=""value"">{alert.Type}</div>
            </div>
            <div class=""field"">
                <div class=""label"">Severity:</div>
                <div class=""value"" style=""color: {severityColor}; font-weight: bold;"">{alert.Severity}</div>
            </div>
            <div class=""field"">
                <div class=""label"">Description:</div>
                <div class=""value"">{alert.Description}</div>
            </div>
            <div class=""field"">
                <div class=""label"">Device ID:</div>
                <div class=""value"">{alert.DeviceId}</div>
            </div>
            <div class=""field"">
                <div class=""label"">Created At:</div>
                <div class=""value"">{alert.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC</div>
            </div>
        </div>
        <div class=""footer"">
            <p>This is an automated alert from SignalBeam Edge Platform.</p>
            <p>Alert ID: {alert.Id}</p>
        </div>
    </div>
</body>
</html>";
    }
}
