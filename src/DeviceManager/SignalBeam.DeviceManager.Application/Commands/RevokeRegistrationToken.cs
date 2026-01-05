using Microsoft.Extensions.Logging;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

/// <summary>
/// Command to revoke a registration token.
/// </summary>
public record RevokeRegistrationTokenCommand(
    Guid TokenId,
    string? RevokedBy = null);

/// <summary>
/// Response for revoking a registration token.
/// </summary>
public record RevokeRegistrationTokenResponse(
    Guid TokenId,
    string Message);

/// <summary>
/// Handler for RevokeRegistrationTokenCommand.
/// </summary>
public class RevokeRegistrationTokenHandler
{
    private readonly IDeviceRegistrationTokenRepository _tokenRepository;
    private readonly ILogger<RevokeRegistrationTokenHandler> _logger;

    public RevokeRegistrationTokenHandler(
        IDeviceRegistrationTokenRepository tokenRepository,
        ILogger<RevokeRegistrationTokenHandler> logger)
    {
        _tokenRepository = tokenRepository;
        _logger = logger;
    }

    public async Task<Result<RevokeRegistrationTokenResponse>> Handle(
        RevokeRegistrationTokenCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get token
            var token = await _tokenRepository.GetByIdAsync(command.TokenId, cancellationToken);

            if (token == null)
            {
                return Result.Failure<RevokeRegistrationTokenResponse>(
                    Error.NotFound(
                        "RegistrationToken.NotFound",
                        $"Registration token with ID {command.TokenId} not found."));
            }

            // Revoke token
            token.Revoke(command.RevokedBy);

            // Save changes
            await _tokenRepository.UpdateAsync(token, cancellationToken);
            await _tokenRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Revoked registration token {TokenId} by {RevokedBy}",
                token.Id,
                command.RevokedBy ?? "Unknown");

            return Result<RevokeRegistrationTokenResponse>.Success(
                new RevokeRegistrationTokenResponse(
                    token.Id,
                    "Registration token has been revoked successfully."));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to revoke registration token {TokenId}: {Message}",
                command.TokenId,
                ex.Message);

            return Result.Failure<RevokeRegistrationTokenResponse>(
                Error.Validation(
                    "RegistrationToken.RevokeFailed",
                    ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error revoking registration token {TokenId}",
                command.TokenId);

            return Result.Failure<RevokeRegistrationTokenResponse>(
                Error.Unexpected(
                    "RegistrationToken.RevokeFailed",
                    "Failed to revoke registration token. Please try again."));
        }
    }
}
