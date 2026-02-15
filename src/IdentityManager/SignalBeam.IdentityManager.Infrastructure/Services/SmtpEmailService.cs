using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SignalBeam.IdentityManager.Application.Services;

namespace SignalBeam.IdentityManager.Infrastructure.Services;

/// <summary>
/// SMTP-based email service for sending invitation emails.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendInvitationEmailAsync(
        string email,
        string tenantName,
        string inviterName,
        string acceptUrl,
        CancellationToken cancellationToken = default)
    {
        var smtpConfig = _configuration.GetSection("Smtp");
        var host = smtpConfig["Host"] ?? "localhost";
        var port = smtpConfig.GetValue("Port", 587);
        var fromAddress = smtpConfig["FromAddress"] ?? "noreply@signalbeam.io";
        var fromName = smtpConfig["FromName"] ?? "SignalBeam";
        var username = smtpConfig["Username"];
        var password = smtpConfig["Password"];

        var encodedInviter = WebUtility.HtmlEncode(inviterName);
        var encodedTenant = WebUtility.HtmlEncode(tenantName);
        var encodedUrl = WebUtility.HtmlEncode(acceptUrl);

        var subject = $"{inviterName} invited you to join {tenantName} on SignalBeam";
        var body = $"""
            <html>
            <body style="font-family: sans-serif; max-width: 600px; margin: 0 auto;">
                <h2>You've been invited!</h2>
                <p><strong>{encodedInviter}</strong> has invited you to join <strong>{encodedTenant}</strong> on SignalBeam Edge.</p>
                <p>Click the link below to accept the invitation:</p>
                <p><a href="{encodedUrl}" style="display: inline-block; padding: 12px 24px; background-color: #7c3aed; color: white; text-decoration: none; border-radius: 6px;">Accept Invitation</a></p>
                <p style="color: #666; font-size: 14px;">This invitation expires in 7 days.</p>
            </body>
            </html>
            """;

        using var client = new SmtpClient(host, port);
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            client.Credentials = new NetworkCredential(username, password);
            client.EnableSsl = true;
        }

        var message = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };
        message.To.Add(email);

        try
        {
            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("Invitation email sent to {Email} for tenant {TenantName}", email, tenantName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send invitation email to {Email}", email);
            throw;
        }
    }
}
