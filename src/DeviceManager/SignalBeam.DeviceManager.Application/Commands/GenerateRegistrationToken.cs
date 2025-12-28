using Microsoft.Extensions.Logging;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Authentication;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

/// <summary>
/// Command to generate a new device registration token.
/// </summary>
public record GenerateRegistrationTokenCommand(
    Guid TenantId,
    int ValidityDays = 30,
    string? CreatedBy = null,
    string? Description = null);

/// <summary>
/// Response containing the generated registration token.
/// </summary>
public record GenerateRegistrationTokenResponse(
    Guid TokenId,
    string Token,
    DateTimeOffset ExpiresAt,
    string Description);

/// <summary>
/// Handler for GenerateRegistrationTokenCommand.
/// </summary>
public class GenerateRegistrationTokenHandler
{
    private readonly IDeviceRegistrationTokenRepository _tokenRepository;
    private readonly IRegistrationTokenService _tokenService;
    private readonly ILogger<GenerateRegistrationTokenHandler> _logger;

    public GenerateRegistrationTokenHandler(
        IDeviceRegistrationTokenRepository tokenRepository,
        IRegistrationTokenService tokenService,
        ILogger<GenerateRegistrationTokenHandler> logger)
    {
        _tokenRepository = tokenRepository;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<Result<GenerateRegistrationTokenResponse>> Handle(
        GenerateRegistrationTokenCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = new TenantId(command.TenantId);

            // Generate token
            var (plainTextToken, tokenHash, tokenPrefix) = _tokenService.GenerateToken();

            // Calculate expiration
            var expiresAt = DateTimeOffset.UtcNow.AddDays(command.ValidityDays);

            // Create token entity
            var token = DeviceRegistrationToken.Create(
                tenantId,
                tokenHash,
                tokenPrefix,
                expiresAt,
                command.CreatedBy,
                command.Description);

            // Save to database
            await _tokenRepository.AddAsync(token, cancellationToken);
            await _tokenRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Generated registration token {TokenId} for tenant {TenantId}, expires at {ExpiresAt}",
                token.Id,
                tenantId,
                expiresAt);

            return Result<GenerateRegistrationTokenResponse>.Success(
                new GenerateRegistrationTokenResponse(
                    token.Id,
                    plainTextToken, // Return plain text token - only shown once!
                    expiresAt,
                    command.Description ?? $"Token expires {expiresAt:yyyy-MM-dd}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate registration token for tenant {TenantId}", command.TenantId);
            return Result.Failure<GenerateRegistrationTokenResponse>(
                Error.Unexpected(
                    "RegistrationToken.GenerationFailed",
                    "Failed to generate registration token. Please try again."));
        }
    }
}
