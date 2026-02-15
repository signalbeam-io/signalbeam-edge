namespace SignalBeam.IdentityManager.Application.Services;

/// <summary>
/// Service for sending emails from the IdentityManager.
/// </summary>
public interface IEmailService
{
    Task SendInvitationEmailAsync(
        string email,
        string tenantName,
        string inviterName,
        string acceptUrl,
        CancellationToken cancellationToken = default);
}
