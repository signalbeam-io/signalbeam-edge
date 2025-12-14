using Microsoft.IdentityModel.Tokens;
using SignalBeam.Shared.Infrastructure.Results;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SignalBeam.Shared.Infrastructure.Authentication;

/// <summary>
/// Default implementation of JWT token validator.
/// </summary>
public sealed class JwtTokenValidator : IJwtTokenValidator
{
    private readonly TokenValidationParameters _validationParameters;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public JwtTokenValidator(TokenValidationParameters validationParameters)
    {
        _validationParameters = validationParameters ?? throw new ArgumentNullException(nameof(validationParameters));
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public async Task<Result<ClaimsPrincipal>> ValidateAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Error.Validation("TOKEN_EMPTY", "Token cannot be empty.");
        }

        try
        {
            var principal = await Task.Run(() =>
                _tokenHandler.ValidateToken(token, _validationParameters, out _),
                cancellationToken);

            return Result.Success(principal);
        }
        catch (SecurityTokenException ex)
        {
            return Error.Unauthorized("INVALID_TOKEN", $"Token validation failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Error.Unexpected("TOKEN_VALIDATION_ERROR", $"Unexpected error during token validation: {ex.Message}");
        }
    }

    public IEnumerable<Claim>? ExtractClaims(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var jwtToken = _tokenHandler.ReadJwtToken(token);
            return jwtToken.Claims;
        }
        catch
        {
            return null;
        }
    }
}
